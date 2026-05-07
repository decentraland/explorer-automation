import { spawn, type ChildProcess } from 'node:child_process'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const __dirname = path.dirname(fileURLToPath(import.meta.url))
const REPO_ROOT = path.resolve(__dirname, '../../../..')
const EXPLORER_TESTS_DIR = path.join(REPO_ROOT, 'explorer', 'Tests')

export interface RunExplorerOptions {
  alttester?: boolean
  /** Wipe the launcher cache before launch (forces logged-out state). */
  clear?: boolean
}

/**
 * Launches the Explorer desktop client via metaforge as a detached child
 * process. The web side has already written `auth-token-bridge.txt`, so the
 * launcher's `TokenFileAuthenticator` will pick it up and skip the login UI.
 *
 * Returns the child process handle so the caller can kill it on test teardown.
 * `metaforge explorer run` exits once it has handed off to the Explorer
 * binary, but we keep the reference for symmetry.
 */
export function runExplorer(options: RunExplorerOptions = {}): ChildProcess {
  const args = ['explorer', 'run']
  if (options.clear) args.push('--clear')
  args.push('--')
  if (options.alttester) args.push('--alttester')

  const child = spawn('metaforge', args, {
    cwd: REPO_ROOT,
    stdio: 'inherit',
    detached: false
  })
  return child
}

/**
 * Shells out to the C# `TestExplorerIsInWorldFromTokenBridge` fixture, which
 * connects via AltTester and asserts the player is in-world. Resolves on
 * exit code 0; rejects with the captured output otherwise.
 */
export function verifyExplorerInWorld(): Promise<void> {
  return new Promise((resolve, reject) => {
    const child = spawn(
      'dotnet',
      [
        'test',
        EXPLORER_TESTS_DIR,
        '--filter',
        'Name=TestExplorerIsInWorldFromTokenBridge',
        '--logger',
        'console;verbosity=normal'
      ],
      { stdio: 'inherit' }
    )
    child.on('error', reject)
    child.on('exit', code => {
      if (code === 0) resolve()
      else reject(new Error(`dotnet test exited with code ${code}`))
    })
  })
}
