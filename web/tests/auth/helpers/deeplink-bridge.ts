import os from 'node:os'
import path from 'node:path'
import fs from 'node:fs/promises'

/**
 * Returns the OS-specific path to `deeplink-bridge.json` — the file the
 * launcher writes after receiving a `decentraland://` OS deep link, and the
 * Explorer's `DeeplinkSentinel` polls for.
 *
 * Format: `{"deeplink": "decentraland://open?signin={identityId}&authRequestId={uuid}"}`
 *
 * The sentinel deletes the file once consumed or after a 300s deferral timeout.
 */
export function getDeeplinkBridgePath(): string {
  switch (process.platform) {
    case 'darwin':
      return path.join(
        os.homedir(),
        'Library',
        'Application Support',
        'DecentralandLauncherLight',
        'deeplink-bridge.json'
      )
    case 'win32':
      return path.join(
        process.env['LOCALAPPDATA'] ?? path.join(os.homedir(), 'AppData', 'Local'),
        'DecentralandLauncherLight',
        'deeplink-bridge.json'
      )
    case 'linux':
      return path.join(
        process.env['XDG_DATA_HOME'] ?? path.join(os.homedir(), '.local', 'share'),
        'DecentralandLauncherLight',
        'deeplink-bridge.json'
      )
    default:
      throw new Error(`Unsupported platform for deeplink bridge: ${process.platform}`)
  }
}

export interface DeeplinkBridgeDTO {
  deeplink: string
}

export async function writeDeeplinkBridge(deeplinkUrl: string): Promise<void> {
  const bridgePath = getDeeplinkBridgePath()
  await fs.mkdir(path.dirname(bridgePath), { recursive: true })
  const dto: DeeplinkBridgeDTO = { deeplink: deeplinkUrl }
  await fs.writeFile(bridgePath, JSON.stringify(dto), 'utf8')
}

export async function removeDeeplinkBridge(): Promise<void> {
  try {
    await fs.unlink(getDeeplinkBridgePath())
  } catch (err) {
    if ((err as NodeJS.ErrnoException).code !== 'ENOENT') throw err
  }
}
