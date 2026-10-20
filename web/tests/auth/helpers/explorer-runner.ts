import { spawn, spawnSync, type ChildProcess } from 'node:child_process'
import fs from 'node:fs'
import net from 'node:net'
import os from 'node:os'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { sleep } from '../../../shared/helpers/async.js'
import { getCrossStackPath } from './persistent-data-path.js'

const __dirname = path.dirname(fileURLToPath(import.meta.url))
const REPO_ROOT = path.resolve(__dirname, '../../../..')
const EXPLORER_TESTS_DIR = path.join(REPO_ROOT, 'explorer', 'Tests')

/**
 * Target Explorer build for AltTester-instrumented runs. Two forms:
 *   - **Absolute path** to a local `.app` bundle (e.g.
 *     `/Users/me/Downloads/Decentraland.app`) — used directly via `open`.
 *   - **Branch/PR target** (e.g. `dev`, `chore/expose-requestid-for-cross-tests`,
 *     a PR number) — resolved via `mf explorer install <target>` and then
 *     launched from the install dir.
 *
 * The build MUST have the `ALTTESTER` preprocessor symbol defined at compile
 * time, otherwise `--alttester` is a no-op and the AltTester driver never
 * instantiates. Stock `latest` releases do NOT have this define; `dev` and
 * dedicated AltTester-enabled branches do.
 *
 * Configurable via `EXPLORER_BUILD_TARGET` env var. Default `dev`.
 */
const EXPLORER_BUILD_TARGET = process.env['EXPLORER_BUILD_TARGET'] ?? 'dev'

let altTesterProc: ChildProcess | undefined
let explorerLaunched = false

/**
 * The MetaForge installer drops `mf` into a path that isn't on the default
 * non-interactive shell's PATH (`~/Library/Application Support/Decentraland/
 * MetaForge/bin` on macOS). Node's `spawn` inherits the parent process env, so
 * if Playwright is launched from a context that doesn't already source the
 * user's shell rc, `mf` won't resolve. Prepend the known macOS install dir to
 * PATH so the spawn finds it either way.
 */
function metaforgeEnv(): NodeJS.ProcessEnv {
  if (process.platform !== 'darwin') return process.env
  const mfBin = path.join(os.homedir(), 'Library', 'Application Support', 'Decentraland', 'MetaForge', 'bin')
  const currentPath = process.env['PATH'] ?? ''
  return { ...process.env, PATH: `${mfBin}:${currentPath}` }
}

export interface RunExplorerOptions {
  alttester?: boolean
  /** Wipe the launcher cache before launch (forces logged-out state). */
  clear?: boolean
}

/**
 * Launches the Explorer desktop client via `mf explorer run`. Only used by
 * Flow 2 (web-first → token-bridge), where the bridge file is written before
 * Explorer is launched. Flow 1 uses `setupExplorerStack` instead.
 */
export function runExplorer(options: RunExplorerOptions = {}): ChildProcess {
  const args = ['explorer', 'run']
  if (options.clear) args.push('--clear')
  args.push('--')
  if (options.alttester) args.push('--alttester')

  return spawn('mf', args, {
    cwd: REPO_ROOT,
    stdio: 'inherit',
    detached: false,
    env: metaforgeEnv()
  })
}

/**
 * One-shot setup of the AltTester stack: AltTester Desktop + the
 * AltTester-instrumented Explorer, both launched in detached background
 * processes that outlive any individual `dotnet test --filter` invocation.
 * After this resolves, multiple `runExplorerTest(...)` calls hit the same
 * long-running stack via plain `dotnet test`.
 *
 * **Why not just use `mf explorer test`?** `mf explorer test` is designed for
 * a single test-run lifecycle: it installs, launches, runs tests, and tears
 * everything down on exit. The `--keep-explorer-open` flag only applies to
 * LocalServer report mode (when mf is also serving Allure). With
 * `--report-type None` (which we want, to keep mf out of Playwright's report
 * pipeline), mf closes Explorer + AltTester at the end of its run. Subsequent
 * `runExplorerTest` calls then get a `NoAppConnectedException`.
 *
 * Lifecycle:
 *   1. Start `mf alttester run` in the background. Wait for port 13000.
 *   2. `open <app-path> --args --skip-version-check --alttester …` for local
 *      builds, or resolve the install path via `mf explorer install` first
 *      for branch/PR targets.
 *   3. Wait for the Explorer ↔ AltTester ESTABLISHED TCP connection on 13000.
 *   4. Stash the AltTester process handle for `teardownExplorerStack` to kill.
 */
export async function setupExplorerStack(): Promise<void> {
  await startAltTesterDesktop()
  await launchInstrumentedExplorer()
}

/**
 * Starts AltTester Desktop on port 13000 WITHOUT launching the Explorer.
 *
 * For Flow 2: the production user journey is web sign-in → click download
 * CTA → open the downloaded `.dmg`. The client's first launch is what
 * reads `auth-token-bridge.txt` and authenticates. Pre-launching the
 * Explorer (as `setupExplorerStack` does for Flow 1) defeats that — the
 * client gets past its TokenFileAuthenticator startup hook before the
 * spec can drop the bridge into place. Flow 2 instead calls
 * `startAltTesterDesktop` in `beforeAll` and `launchInstrumentedExplorer`
 * in the test body, AFTER the bridge file is on disk.
 */
export async function startAltTesterDesktop(): Promise<void> {
  altTesterProc = spawn('mf', ['alttester', 'run'], {
    cwd: REPO_ROOT,
    stdio: 'ignore',
    env: metaforgeEnv(),
    detached: false
  })
  altTesterProc.on('error', err => {
    console.error('mf alttester run failed:', err)
  })

  await waitForPort('127.0.0.1', 13000, 60_000)
}

/**
 * Launches the AltTester-instrumented Explorer and waits for it to
 * ESTABLISH a TCP connection to AltTester Desktop on port 13000.
 *
 * For Flow 2's correct ordering: call this AFTER writing
 * `auth-token-bridge.txt`. The first-launch hook reads the bridge,
 * authenticates, lands the user on the cached-account "Welcome back" sub-
 * screen — at which point the C# `WebFirstLoginUsernameAssert` fixture
 * verifies the identity matches the signup username.
 */
export async function launchInstrumentedExplorer(): Promise<void> {
  await openExplorerApp('launch')
  await waitForEstablishedConnection(13000, 180_000)
  explorerLaunched = true
}

/**
 * Resolves the target Explorer `.app` (a local-build path or an mf-installed
 * branch/PR build) and launches it via `open` with the standard
 * AltTester-instrumented args. Shared by the initial launch and the mid-flow
 * relaunch so the launch flags can't drift between the two call sites.
 * `context` only labels the error message ("launch" / "relaunch").
 */
async function openExplorerApp(context: string): Promise<void> {
  const isLocalBuild = EXPLORER_BUILD_TARGET.startsWith('/')
  const appPath = isLocalBuild ? EXPLORER_BUILD_TARGET : await resolveInstalledExplorerPath(EXPLORER_BUILD_TARGET)

  const openResult = spawnSync(
    'open',
    [appPath, '--args', '--skip-version-check', '--position', '100,100', '--dclenv', 'org', '--alttester'],
    { stdio: 'inherit' }
  )
  if (openResult.status !== 0) {
    throw new Error(`open ${appPath} (${context}) failed with status ${openResult.status}`)
  }
}

/**
 * Wipes the Explorer-side identity cache (Thirdweb EcosystemWallet) so that
 * the next launch can't authenticate against a prior session and ignore the
 * `auth-token-bridge.txt` we just wrote. Without this, Flow 2 may
 * silently boot the cached user instead of the freshly-issued bridge
 * identity — and the C# identity guard would either fail (correct) or
 * misleadingly pass against the wrong account.
 *
 * Safe to call when the cache is already absent. macOS-specific path.
 */
export function clearExplorerIdentityCache(): void {
  if (process.platform !== 'darwin') return
  // Same Unity persistentDataPath base as the auth-url / verification-code
  // bridges (see persistent-data-path.ts) — single source of truth for the
  // `Decentraland/Explorer/` dir.
  const thirdwebDir = getCrossStackPath('explorer', path.join('Thirdweb', 'EcosystemWallet'))
  // rmSync(recursive, force) removes the dir and everything under it: tolerant
  // of nested subdirectories (the old per-file unlinkSync threw EISDIR on
  // those) and of an already-absent dir (force suppresses ENOENT). Thirdweb
  // recreates the dir on the next launch, so there's nothing to restore.
  fs.rmSync(thirdwebDir, { recursive: true, force: true })
}

/**
 * Kills the running instrumented Explorer (leaving AltTester Desktop up) and
 * relaunches it. Required between stages of Flow 2: the Explorer reads
 * `auth-token-bridge.txt` ONLY on startup via `TokenFileAuthenticator`, so a
 * bridge written after the initial `setupExplorerStack` launch has no effect
 * without a restart. The relaunch re-establishes the AltTester driver
 * connection on port 13000 — subsequent `runExplorerTest` calls hit the
 * freshly-authenticated client.
 */
export async function relaunchExplorer(): Promise<void> {
  spawnSync('pkill', ['-f', 'Decentraland.app/Contents/MacOS/Explorer'], { stdio: 'ignore' })

  // Wait for the AltTester ESTABLISHED connection to drop. Otherwise the next
  // `open` may race against the dying socket, and the new driver could attach
  // to a stale handle that AltTester Desktop hasn't yet released.
  await waitForNoEstablishedConnection(13000, 30_000)

  await openExplorerApp('relaunch')
  await waitForEstablishedConnection(13000, 180_000)
}

/**
 * Tears down AltTester Desktop + Explorer. Safe to call multiple times.
 * Intended to run in a Playwright `afterAll` hook.
 */
export function teardownExplorerStack(): void {
  if (explorerLaunched) {
    spawnSync('pkill', ['-f', 'Decentraland.app/Contents/MacOS/Explorer'], { stdio: 'ignore' })
    explorerLaunched = false
  }
  if (altTesterProc && !altTesterProc.killed) {
    altTesterProc.kill('SIGTERM')
    altTesterProc = undefined
  }
}

/**
 * Resolves a non-local `EXPLORER_BUILD_TARGET` (branch / PR / version tag) to
 * the local installed `.app` path. Installs the build via `mf explorer install
 * <target>` first if it isn't present.
 *
 * Returns the absolute path to the `.app` bundle.
 */
async function resolveInstalledExplorerPath(target: string): Promise<string> {
  // mf explorer install is idempotent — it skips download if the build is
  // already present, but always prints the install path.
  const install = spawnSync('mf', ['explorer', 'install', target, '--non-interactive'], {
    cwd: REPO_ROOT,
    env: metaforgeEnv(),
    encoding: 'utf8'
  })
  if (install.status !== 0) {
    throw new Error(`mf explorer install ${target} failed: ${install.stderr || install.stdout}`)
  }

  const installRoot = path.join(os.homedir(), 'Library', 'Application Support', 'Decentraland', 'MetaForge', 'explorer')

  // mf colourises + soft-wraps "installed at: <path>" with ANSI codes; the
  // path can contain spaces ("Application Support") and arbitrary line
  // breaks. Strip ANSI + collapse whitespace before matching, then extract
  // everything from `installed at:` up to the next mf log header.
  // eslint-disable-next-line no-control-regex -- intentional: \x1b is the ANSI CSI lead byte that mf emits.
  const clean = install.stdout.replace(/\x1b\[[0-9;]*[A-Za-z]/g, '').replace(/\s+/g, ' ')
  const match = clean.match(/installed at:\s*(\/.+?)(?=\s+(?:Clearing|Explorer\b|$))/)
  if (match) return path.join(match[1]!.trim(), 'Decentraland.app')

  // Fallback: enumerate the install root and pick the most-recently-modified
  // entry whose name is `<target>` or `<target>_<hash>` (mf appends a
  // short commit hash to branch/PR installs).
  try {
    const entries = fs.readdirSync(installRoot, { withFileTypes: true })
    const matching = entries
      .filter(e => e.isDirectory() && (e.name === target || e.name.startsWith(`${target}_`)))
      .map(e => {
        const full = path.join(installRoot, e.name)
        return { full, mtime: fs.statSync(full).mtimeMs }
      })
      .sort((a, b) => b.mtime - a.mtime)
    if (matching.length > 0) return path.join(matching[0]!.full, 'Decentraland.app')
  } catch {
    // fall through to the legacy fallback below
  }

  // Legacy fallback: assume the install dir matches the target name verbatim.
  return path.join(installRoot, target, 'Decentraland.app')
}

/**
 * Polls for a TCP listener on `host:port` until it accepts a connection or
 * `timeoutMs` elapses.
 */
async function waitForPort(host: string, port: number, timeoutMs: number): Promise<void> {
  const deadline = Date.now() + timeoutMs
  while (Date.now() < deadline) {
    const open = await new Promise<boolean>(resolve => {
      const sock = new net.Socket()
      sock.setTimeout(1000)
      sock
        .once('connect', () => {
          sock.destroy()
          resolve(true)
        })
        .once('error', () => {
          sock.destroy()
          resolve(false)
        })
        .once('timeout', () => {
          sock.destroy()
          resolve(false)
        })
        .connect(port, host)
    })
    if (open) return
    await sleep(500)
  }
  throw new Error(`Timed out waiting for ${host}:${port} after ${timeoutMs / 1000}s`)
}

/**
 * Polls `lsof -nP -iTCP:<port>` for an ESTABLISHED connection (in addition to
 * the LISTEN socket AltTester Desktop opened). Signals that Explorer's
 * AltTester driver has successfully connected.
 */
async function waitForEstablishedConnection(port: number, timeoutMs: number): Promise<void> {
  const deadline = Date.now() + timeoutMs
  while (Date.now() < deadline) {
    const res = spawnSync('lsof', ['-nP', `-iTCP:${port}`], { encoding: 'utf8' })
    if (res.stdout?.includes('ESTABLISHED')) return
    await sleep(2000)
  }
  throw new Error(`Explorer did not establish a connection to AltTester on :${port} within ${timeoutMs / 1000}s`)
}

async function waitForNoEstablishedConnection(port: number, timeoutMs: number): Promise<void> {
  const deadline = Date.now() + timeoutMs
  while (Date.now() < deadline) {
    const res = spawnSync('lsof', ['-nP', `-iTCP:${port}`], { encoding: 'utf8' })
    if (!res.stdout?.includes('ESTABLISHED')) return
    await sleep(1000)
  }
  throw new Error(`Explorer connection to AltTester on :${port} did not drop within ${timeoutMs / 1000}s`)
}

/**
 * Shells out to a C# test filter expression. Resolves on exit code 0; rejects otherwise.
 */
function runDotnetVerification(filter: string): Promise<void> {
  return new Promise((resolve, reject) => {
    const child = spawn(
      'dotnet',
      ['test', EXPLORER_TESTS_DIR, '--filter', filter, '--logger', 'console;verbosity=normal'],
      { stdio: 'inherit' }
    )
    child.on('error', reject)
    child.on('exit', code => {
      if (code === 0) resolve()
      else reject(new Error(`dotnet test (${filter}) exited with code ${code}`))
    })
  })
}

/**
 * Shells out a single C# NUnit fixture against the already-running Explorer
 * via `dotnet test --filter Name=<filterName>`. Mirrors the manual invocation
 * pattern from `explorer/CLAUDE.md`. Requires `setupExplorerStack()` to have
 * run earlier in the same Playwright process.
 */
export function runExplorerTest(filterName: string): Promise<void> {
  return runDotnetVerification(`Name=${filterName}`)
}

/**
 * Runs the full deeplink login verification suite: in-world state, authenticated
 * session (profile menu / sign-out), backpack, and navmap accessibility.
 */
export function verifyExplorerInWorldFromDeeplink(): Promise<void> {
  return runDotnetVerification('FullyQualifiedName~DeeplinkLoginVerificationTests')
}
