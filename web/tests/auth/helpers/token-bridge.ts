import os from 'node:os'
import path from 'node:path'
import fs from 'node:fs/promises'

/**
 * Returns the OS-specific path to `auth-token-bridge.txt` — the integration
 * point between the web dapp (writer) and the desktop client (reader/consumer).
 *
 * The Decentraland Launcher's `TokenFileAuthenticator` reads this file on
 * startup and deletes it after consuming the token. macOS is the only platform
 * we currently support for cross tests.
 */
export function getTokenBridgePath(): string {
  switch (process.platform) {
    case 'darwin':
      return path.join(
        os.homedir(),
        'Library',
        'Application Support',
        'DecentralandLauncherLight',
        'auth-token-bridge.txt'
      )
    case 'win32':
      return path.join(
        process.env['APPDATA'] ?? path.join(os.homedir(), 'AppData', 'Roaming'),
        'DecentralandLauncherLight',
        'auth-token-bridge.txt'
      )
    case 'linux':
      return path.join(
        process.env['XDG_CONFIG_HOME'] ?? path.join(os.homedir(), '.config'),
        'DecentralandLauncherLight',
        'auth-token-bridge.txt'
      )
    default:
      throw new Error(`Unsupported platform for token bridge: ${process.platform}`)
  }
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

function sleep(ms: number): Promise<void> {
  return new Promise(resolve => setTimeout(resolve, ms))
}
