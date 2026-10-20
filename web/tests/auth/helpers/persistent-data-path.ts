import os from 'node:os'
import path from 'node:path'

/**
 * The two macOS data directories the cross-stack `@cross` flows read from /
 * write to, mapped to their `~/Library/Application Support/` sub-path segments.
 *
 *   - **explorer** → Unity's `Application.persistentDataPath`. For the build
 *     under test (company "Decentraland", product "Explorer" in
 *     `ProjectSettings.asset`) this is
 *     `~/Library/Application Support/Decentraland/Explorer/`.
 *
 *     VERIFIED AT RUNTIME (not assumed): that directory holds Unity's own
 *     runtime artifacts — `analytics_queue_v1.sqlite3`, `Sentry/`, `Thirdweb/`,
 *     `userdata_*.json`, `instance.lock` — alongside the `auth-url.txt` the
 *     ALTTESTER-gated `UnityAppWebBrowser.OpenUrl` hook writes (a file only
 *     ever *read* by the C#/TS sides, so its presence here proves Unity wrote
 *     it here). The sibling `com.Decentraland.Explorer` PlayerPrefs plist dir
 *     independently confirms the product name is "Explorer".
 *
 *     It is NOT `Decentraland Unity Explorer` — that product name belongs to a
 *     different build configuration; no such directory exists for this build.
 *     Re-pointing these constants there would make the pollers watch a
 *     directory Unity never writes to, and every `@cross` flow would time out.
 *
 *   - **launcher** → the Decentraland launcher's own data dir
 *     `~/Library/Application Support/DecentralandLauncherLight/` (the Rust
 *     launcher, a different app from Unity — its `TokenFileAuthenticator`
 *     consumes `auth-token-bridge.txt` on startup).
 */
const APP_DIRS = {
  explorer: ['Decentraland', 'Explorer'],
  launcher: ['DecentralandLauncherLight']
} as const

export type CrossStackApp = keyof typeof APP_DIRS

/**
 * Resolves the absolute path to a cross-stack handoff artifact (`filename`)
 * under the given `app`'s data directory.
 *
 * **macOS only — by design.** The whole `@cross` runner is macOS-bound:
 * `explorer-runner.ts` drives the client with `open` / `pkill` / `lsof` and
 * resolves every install path under `~/Library/Application Support`. Rather
 * than ship untested — and, for the previous Windows branch, *wrong* — path
 * logic, we throw off-macOS. (Unity's `persistentDataPath` maps to
 * `%USERPROFILE%\AppData\LocalLow\<company>\<product>` on Windows — LocalLow,
 * not the `%APPDATA%`/Roaming the old branch used — and to
 * `~/.config/unity3d/<company>/<product>` on Linux; the launcher dir follows
 * its own per-OS convention. Add real branches here when the runner itself
 * becomes cross-platform, not before.)
 */
export function getCrossStackPath(app: CrossStackApp, filename: string): string {
  if (process.platform !== 'darwin') {
    throw new Error(
      `Cross-stack handoff files are only supported on macOS; got platform "${process.platform}". ` +
        'The @cross runner (explorer-runner.ts) uses open/pkill/lsof and macOS install paths. ' +
        'Add Windows (%LOCALAPPDATA%\\..\\LocalLow) / Linux (~/.config/unity3d) branches in ' +
        'persistent-data-path.ts when the runner becomes cross-platform.'
    )
  }
  return path.join(os.homedir(), 'Library', 'Application Support', ...APP_DIRS[app], filename)
}
