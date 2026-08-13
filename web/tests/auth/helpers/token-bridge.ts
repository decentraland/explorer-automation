import path from 'node:path'
import fs from 'node:fs/promises'
import { sleep } from '../../../shared/helpers/async.js'
import { getCrossStackPath } from './persistent-data-path.js'

/**
 * Returns the OS-specific path to `auth-token-bridge.txt` — the integration
 * point between the web dapp (writer) and the desktop client (reader/consumer).
 *
 * The Decentraland Launcher's `TokenFileAuthenticator` reads this file on
 * startup and deletes it after consuming the token. macOS is the only platform
 * we currently support for cross tests.
 */
export function getTokenBridgePath(): string {
  return getCrossStackPath('launcher', 'auth-token-bridge.txt')
}

export async function tokenBridgeExists(): Promise<boolean> {
  try {
    await fs.access(getTokenBridgePath())
    return true
  } catch {
    return false
  }
}

export async function readTokenBridge(): Promise<string> {
  return fs.readFile(getTokenBridgePath(), 'utf8')
}

/**
 * Polls until the dapp writes the token bridge file, then returns its contents.
 * Throws if the timeout elapses without the file appearing.
 */
export async function waitForTokenBridge(timeoutMs = 30_000, pollIntervalMs = 500): Promise<string> {
  const deadline = Date.now() + timeoutMs
  while (Date.now() < deadline) {
    if (await tokenBridgeExists()) {
      return readTokenBridge()
    }
    await sleep(pollIntervalMs)
  }
  throw new Error(`Token bridge file did not appear at ${getTokenBridgePath()} within ${timeoutMs / 1000}s`)
}

/**
 * Removes the token bridge if present. Useful at the start of an `@cross` test
 * to guarantee we observe a freshly-written file rather than a stale one from
 * a previous run.
 */
export async function removeTokenBridge(): Promise<void> {
  try {
    await fs.unlink(getTokenBridgePath())
  } catch (err) {
    if ((err as NodeJS.ErrnoException).code !== 'ENOENT') throw err
  }
}

/**
 * Writes the auth-token-bridge.txt the Decentraland Launcher's
 * `TokenFileAuthenticator` consumes on startup. Used by the Flow 2 spec to
 * hand off an auth token obtained from the download-gateway exchange — the
 * test cuts out the launcher's xattr-read + HTTP-exchange step (which it
 * performs by hand via `helpers/download-gateway.ts`) and writes the result
 * directly to the file the Explorer ultimately reads.
 *
 * The launcher data dir doesn't exist on a clean machine; create it. The
 * 0600 mode keeps the auth token readable by the owner only.
 */
export async function writeTokenBridge(contents: string): Promise<void> {
  const target = getTokenBridgePath()
  await fs.mkdir(path.dirname(target), { recursive: true })
  await fs.writeFile(target, contents, { encoding: 'utf8', mode: 0o600 })
}

/**
 * Path the Flow 2 spec writes the expected-username to, and the C# fixture
 * `WebFirstLoginUsernameAssert::TestInWorldUsernameMatches` reads. Sibling
 * of `auth-token-bridge.txt` — same OS-specific launcher data dir, same
 * write-from-Playwright/read-from-C# pattern as the auth bridge itself.
 *
 * The assertion exists because "Explorer reached in-world" alone can't
 * distinguish a fresh authenticated boot from the launcher silently
 * consuming a stale bridge file or reusing a cached profile.
 */
export function getExpectedUsernamePath(): string {
  return getCrossStackPath('launcher', 'expected-username.txt')
}

export async function writeExpectedUsername(name: string): Promise<void> {
  const target = getExpectedUsernamePath()
  await fs.mkdir(path.dirname(target), { recursive: true })
  await fs.writeFile(target, name, 'utf8')
}

export async function removeExpectedUsername(): Promise<void> {
  try {
    await fs.unlink(getExpectedUsernamePath())
  } catch (err) {
    if ((err as NodeJS.ErrnoException).code !== 'ENOENT') throw err
  }
}
