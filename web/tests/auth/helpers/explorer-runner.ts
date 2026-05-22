import { spawn, spawnSync, type ChildProcess } from 'node:child_process'
import net from 'node:net'
import os from 'node:os'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

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
  const isLocalBuild = EXPLORER_BUILD_TARGET.startsWith('/')

  // Step 1: AltTester Desktop. Long-running (blocks until Ctrl+C). Run it as
  // a detached background child; the OS will reap it when the parent exits,
  // but we also kill it explicitly in teardown.
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

  // Step 2: resolve Explorer .app path, then launch.
  const appPath = isLocalBuild ? EXPLORER_BUILD_TARGET : await resolveInstalledExplorerPath(EXPLORER_BUILD_TARGET)

  // `open` returns immediately; the launched .app runs detached.
  const openResult = spawnSync(
    'open',
    [appPath, '--args', '--skip-version-check', '--position', '100,100', '--dclenv', 'org', '--alttester'],
    { stdio: 'inherit' }
  )
  if (openResult.status !== 0) {
    throw new Error(`open ${appPath} failed with status ${openResult.status}`)
  }

  // Step 3: wait for Explorer's AltTester driver to ESTABLISH a connection to
  // Desktop on 13000. Until this happens, dotnet test invocations will hit
  // NoAppConnectedException.
  await waitForEstablishedConnection(13000, 180_000)
  explorerLaunched = true
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
  // Output contains "Explorer v0.149.0-alpha installed at: <path>" — extract.
  const match = install.stdout.match(/installed at:\s*(\S+)/i)
  if (match) return path.join(match[1]!, 'Decentraland.app')

  // Fallback: well-known macOS install dir layout.
  const installRoot = path.join(os.homedir(), 'Library', 'Application Support', 'Decentraland', 'MetaForge', 'explorer')
  // Pick the most recent dir matching the target name.
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

function sleep(ms: number): Promise<void> {
  return new Promise(resolve => setTimeout(resolve, ms))
}

/**
 * Shells out a single C# NUnit fixture against the already-running Explorer
 * via `dotnet test --filter Name=<filterName>`. Mirrors the manual invocation
 * pattern from `explorer/CLAUDE.md`. Requires `setupExplorerStack()` to have
 * run earlier in the same Playwright process.
 */
export function runExplorerTest(filterName: string): Promise<void> {
  return new Promise((resolve, reject) => {
    const child = spawn(
      'dotnet',
      ['test', EXPLORER_TESTS_DIR, '--filter', `Name=${filterName}`, '--logger', 'console;verbosity=normal'],
      { stdio: 'inherit' }
    )
    child.on('error', reject)
    child.on('exit', code => {
      if (code === 0) resolve()
      else reject(new Error(`dotnet test (${filterName}) exited with code ${code}`))
    })
  })
}
