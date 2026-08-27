/**
 * Extracts the auth-token value the Decentraland launcher would derive from a
 * per-user download URL minted by the dapp post-signup.
 *
 * **There is no network exchange.** Verified against `launcher-rust` v1.16.0
 * (`core/src/auto_auth.rs::DownloadOriginData::from_url`): the launcher only
 * does client-side parsing of the `com.apple.metadata:kMDItemWhereFroms` xattr
 * that browsers stamp on downloaded files. The first value matching the
 * canonical 8-4-4-4-12 UUID regex — query params first (excluding
 * `anon_user_id`), then path segments — is treated as the auth token and
 * written verbatim to `~/Library/Application Support/DecentralandLauncherLight/
 * auth-token-bridge.txt`. The launcher reads it back on first launch and
 * passes it to the desktop client; the client's `TokenFileAuthenticator`
 * consumes it the same way it would consume a normal auth token.
 *
 * Implication: our Flow 2 spec captures the `download.url()` Playwright surfaces
 * when the dapp's personalised download CTA fires, runs the same UUID-pick
 * algorithm here, and writes the result through `writeTokenBridge` — skipping
 * the .dmg/launcher entirely while still exercising the dapp's URL-mint
 * contract that the launcher depends on.
 *
 * Example input/output (real dapp-generated URL, observed May 2026):
 *
 *   https://download-gateway.decentraland.org/be327897-568d-412b-a915-e26487b50e70/decentraland.dmg?anon_user_id=8e6781b5-a7cf-4f5b-bfb2-bc6e2fa8be5e
 *   → 'be327897-568d-412b-a915-e26487b50e70'  (path segment wins; anon_user_id is excluded)
 */
const UUID_RE = /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/

export function parseAuthTokenFromDownloadUrl(downloadUrl: string): string {
  const url = new URL(downloadUrl)

  for (const [key, value] of url.searchParams) {
    if (key === 'anon_user_id') continue
    if (UUID_RE.test(value)) return value
  }

  for (const segment of url.pathname.split('/')) {
    if (UUID_RE.test(segment)) return segment
  }

  throw new Error(
    `No auth-token UUID found in download URL: ${downloadUrl}. ` +
      'Expected a 8-4-4-4-12 UUID in either a non-anon_user_id query value or a path segment ' +
      '(the dapp may have changed its URL shape — check launcher-rust core/src/auto_auth.rs).'
  )
}
