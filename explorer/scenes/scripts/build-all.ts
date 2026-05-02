/**
 * Builds every scene in `packages/*` in parallel via a bounded pool.
 *
 *   npm run build                       # default concurrency, fail-fast
 *   BUILD_CONCURRENCY=8 npm run build   # override pool size
 *   BUILD_FAIL_FAST=0 npm run build     # report every failure instead of aborting
 */
import { spawn } from 'node:child_process'
import { readdirSync, statSync } from 'node:fs'
import { join } from 'node:path'
import { runPool, type JobResult } from './pool.ts'

const ROOT = new URL('..', import.meta.url).pathname
const PACKAGES = join(ROOT, 'packages')
const concurrency = Number(process.env.BUILD_CONCURRENCY ?? 5)
const failFast = process.env.BUILD_FAIL_FAST !== '0'

function listScenes(): string[] {
  return readdirSync(PACKAGES)
    .filter((name) => !name.startsWith('.'))
    .filter((name) => statSync(join(PACKAGES, name)).isDirectory())
}

function run(cmd: string, args: string[], cwd: string): Promise<void> {
  return new Promise((resolve, reject) => {
    const child = spawn(cmd, args, { cwd, stdio: ['ignore', 'pipe', 'pipe'] })
    let buffer = ''
    child.stdout.on('data', (d) => (buffer += d))
    child.stderr.on('data', (d) => (buffer += d))
    child.on('error', reject)
    child.on('close', (code) =>
      code === 0
        ? resolve()
        : reject(new Error(`${cmd} ${args.join(' ')} exited ${code}\n${buffer.slice(-2000)}`))
    )
  })
}

const scenes = listScenes()
if (scenes.length === 0) {
  process.stderr.write('No scenes found in packages/.\n')
  process.exit(0)
}

process.stderr.write(`Building ${scenes.length} scenes (concurrency=${concurrency}, fail-fast=${failFast}).\n\n`)

const results = (await runPool(
  scenes.map((name) => ({
    label: name,
    fn: () => run('npm', ['run', 'build'], join(PACKAGES, name))
  })),
  {
    concurrency,
    failFast,
    onStart: (label) => process.stderr.write(`  build ${label}…\n`),
    onFinish: (r) => {
      const tag = r.ok ? '\x1b[32m✓\x1b[0m' : '\x1b[31m✗\x1b[0m'
      process.stderr.write(`  ${tag} ${r.label}  (${r.ms}ms)\n`)
      if (!r.ok && r.error) process.stderr.write(`    ${r.error.message.split('\n').slice(0, 1).join('\n')}\n`)
    }
  }
).catch((err: Error) => {
  process.stderr.write(`\n\x1b[31mBuild aborted: ${err.message}\x1b[0m\n`)
  process.exit(1)
})) as JobResult<void>[]

const failed = results.filter((r) => !r.ok)
process.stderr.write(`\nDone. ${results.length - failed.length} ok, ${failed.length} failed.\n`)
if (failed.length > 0) {
  for (const f of failed) {
    process.stderr.write(`\n--- ${f.label} ---\n${f.error?.message}\n`)
  }
  process.exit(1)
}
