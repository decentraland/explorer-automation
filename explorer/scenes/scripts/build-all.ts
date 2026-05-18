/**
 * Builds every scene in `packages/*` in parallel via a bounded pool.
 *
 *   npm run build                       # default concurrency, fail-fast
 *   BUILD_CONCURRENCY=8 npm run build   # override pool size
 *   BUILD_FAIL_FAST=0 npm run build     # report every failure instead of aborting
 *   BUILD_TIMEOUT_MS=600000 npm run build  # hard kill any single scene build that exceeds this
 */
import { spawn, spawnSync } from 'node:child_process'
import { readdirSync, statSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { runPool, type JobResult } from './pool.ts'

const ROOT = fileURLToPath(new URL('..', import.meta.url))
const PACKAGES = join(ROOT, 'packages')
const concurrency = Number(process.env.BUILD_CONCURRENCY ?? 5)
const failFast = process.env.BUILD_FAIL_FAST !== '0'
const timeoutMs = Number(process.env.BUILD_TIMEOUT_MS ?? 10 * 60 * 1000)

// On Windows `npm` is shipped as three files in the Node install dir:
//   npm       (POSIX shell script — useless to Node's spawn)
//   npm.cmd   (Windows batch — what cmd.exe runs)
//   npm.ps1   (PowerShell)
// `where npm` returns all three, and the first match is often the bare
// extensionless one which `spawn` can't execute (ENOENT). We need to pick
// `npm.cmd` explicitly. On POSIX, `which npm` returns a single path that
// already runs as-is.
function resolveNpm(): string {
  const isWin = process.platform === 'win32'
  const which = isWin ? 'where' : 'which'
  const r = spawnSync(which, ['npm'], { encoding: 'utf8' })
  if (r.status === 0) {
    const lines = r.stdout.split(/\r?\n/).map((l) => l.trim()).filter(Boolean)
    if (isWin) {
      const preferredExts = ['.cmd', '.exe', '.bat']
      for (const ext of preferredExts) {
        const match = lines.find((l) => l.toLowerCase().endsWith(ext))
        if (match) return match
      }
      if (lines[0]) return lines[0] + '.cmd'
    } else {
      if (lines[0]) return lines[0]
    }
  }

  return isWin ? 'npm.cmd' : 'npm'
}
const NPM = resolveNpm()

function listScenes(): string[] {
  return readdirSync(PACKAGES)
    .filter((name) => !name.startsWith('.'))
    .filter((name) => statSync(join(PACKAGES, name)).isDirectory())
}

function run(cmd: string, args: string[], cwd: string, signal: AbortSignal): Promise<void> {
  return new Promise((resolve, reject) => {
    if (signal.aborted) {
      reject(new Error(`${cmd} ${args.join(' ')} aborted before start`))
      return
    }

    const isWin = process.platform === 'win32'
    const cmdToSpawn = isWin && /\s/.test(cmd) ? `"${cmd}"` : cmd
    const child = spawn(cmdToSpawn, args, {
      cwd,
      stdio: ['ignore', 'pipe', 'pipe'],
      detached: !isWin,
      shell: isWin,
      windowsHide: true,
    })
    let buffer = ''
    child.stdout.on('data', (d) => (buffer += d))
    child.stderr.on('data', (d) => (buffer += d))

    let timedOut = false
    // Tear down the build process tree. POSIX uses a process group + negative
    // pid so npm + its tsc/esbuild children all receive the signal at once;
    // without that, killing npm leaves orphan node processes wedged. Windows
    // doesn't have process groups in the same form — taskkill /T /F walks
    // the parent-child tree directly, which is the closest equivalent.
    const killTree = (signalName: NodeJS.Signals) => {
      if (!child.pid) return
      if (isWin) {
        try {
          spawnSync('taskkill', ['/PID', String(child.pid), '/T', '/F'], { stdio: 'ignore' })
        } catch { /* already gone */ }
      } else {
        try {
          process.kill(-child.pid, signalName)
        } catch {
          try { child.kill(signalName) } catch { /* already gone */ }
        }
      }
    }

    const onAbort = () => killTree('SIGTERM')
    signal.addEventListener('abort', onAbort, { once: true })

    const timeout = setTimeout(() => {
      timedOut = true
      killTree('SIGTERM')
      // 5s grace for graceful shutdown, then SIGKILL the group
      setTimeout(() => killTree('SIGKILL'), 5_000).unref()
    }, timeoutMs)

    child.on('error', (err) => {
      clearTimeout(timeout)
      signal.removeEventListener('abort', onAbort)
      reject(err)
    })
    child.on('close', (code, killSignal) => {
      clearTimeout(timeout)
      signal.removeEventListener('abort', onAbort)
      if (timedOut) {
        reject(new Error(`${cmd} ${args.join(' ')} timed out after ${timeoutMs}ms\n${buffer.slice(-4000)}`))
        return
      }
      if (signal.aborted) {
        reject(new Error(`${cmd} ${args.join(' ')} aborted (signal=${killSignal ?? 'none'})`))
        return
      }
      if (code === 0) {
        resolve()
      } else {
        reject(new Error(`${cmd} ${args.join(' ')} exited ${code}\n${buffer.slice(-4000)}`))
      }
    })
  })
}

const scenes = listScenes()
if (scenes.length === 0) {
  process.stderr.write('No scenes found in packages/.\n')
  process.exit(0)
}

process.stderr.write(`Building ${scenes.length} scenes (concurrency=${concurrency}, fail-fast=${failFast}).\n\n`)

let results: JobResult<void>[]
try {
  results = await runPool(
    scenes.map((name) => ({
      label: name,
      fn: (signal: AbortSignal) => run(NPM, ['run', 'build'], join(PACKAGES, name), signal)
    })),
    {
      concurrency,
      failFast,
      onStart: (label) => process.stderr.write(`  build ${label}…\n`),
      onFinish: (r) => {
        const tag = r.ok ? '\x1b[32m✓\x1b[0m' : '\x1b[31m✗\x1b[0m'
        process.stderr.write(`  ${tag} ${r.label}  (${r.ms}ms)\n`)
      }
    }
  )
} catch (err) {
  const message = err instanceof Error ? err.message : String(err)
  process.stderr.write(`\n\x1b[31mBuild aborted:\x1b[0m\n${message}\n`)
  process.exit(1)
}

const failed = results.filter((r) => !r.ok)
process.stderr.write(`\nDone. ${results.length - failed.length} ok, ${failed.length} failed.\n`)
if (failed.length > 0) {
  for (const f of failed) {
    process.stderr.write(`\n--- ${f.label} ---\n${f.error?.message ?? '(no error)'}\n`)
  }
  process.exit(1)
}
