import type { Page } from '@playwright/test'

/**
 * Page Object for the post-signup landing area at `decentraland.<tld>/`.
 *
 * After OTP signup (and QuickSetup for genuinely-new users), the dapp lands
 * the browser on `https://decentraland.<tld>/`. The hero on that page
 * carries a "DOWNLOAD FOR <OS>" CTA — the same element shown to anonymous
 * visitors, but when an authenticated session is active the dapp's JS
 * attached to that CTA fetches a **personalised** download URL of the
 * shape:
 *
 *   https://download-gateway.decentraland.org/<UUID>/decentraland.dmg
 *
 * The path-segment UUID is the auth-token-redeemable handle the launcher's
 * `auto_auth` module would normally extract from the
 * `com.apple.metadata:kMDItemWhereFroms` xattr on first launch (per
 * `launcher-rust` `core/src/auto_auth.rs`).
 *
 * Playwright's `download.url()` is unhelpful here: the dapp `fetch`-es the
 * .dmg, wraps the bytes in a Blob, and triggers the download via
 * `URL.createObjectURL` — so `download.url()` returns a `blob:` URL with no
 * trace of the gateway URL. The right capture point is `page.on('request')`
 * — the underlying outbound request to download-gateway is observable
 * regardless of how the dapp routes the resulting bytes.
 *
 * Locator priority per `web/CLAUDE.md` auth surface
 * (`getByRole > getByText > getByTestId > CSS`):
 *   - The CTA is an `<a class="hero-download-btn" href="/download">…`. The
 *     accessible name varies by detected OS — "DOWNLOAD FOR macOS",
 *     "DOWNLOAD FOR Windows" — so we match `/^DOWNLOAD FOR/i` and take the
 *     first hit. The `.first()` is necessary because the page also renders
 *     a secondary CTA with an `os=…` query in the href (handled by
 *     `LandingPage.downloadLauncher`) whose accessible name also starts
 *     with "DOWNLOAD FOR"; DOM order puts the hero CTA first.
 */
export class PostSignupPage {
  constructor(private readonly page: Page) {}

  /**
   * Clicks the post-signup personalised download CTA and resolves with the
   * outbound URL hit on `download-gateway.decentraland.<tld>`. The returned
   * URL contains the auth-token UUID in the path; pass it to
   * `parseAuthTokenFromDownloadUrl` to extract.
   *
   * Throws if no gateway request is observed within `timeoutMs`. The
   * gateway request fires roughly synchronously with the click — generous
   * default to absorb the page's startup JS warm-up on a cold cache.
   */
  async captureSignedDownloadUrl(timeoutMs = 30_000): Promise<string> {
    const deadline = Date.now() + timeoutMs
    let captured: string | undefined
    const listener = (req: { url: () => string }): void => {
      const u = req.url()
      if (u.includes('download-gateway.decentraland.')) captured = u
    }
    this.page.on('request', listener)
    try {
      // The CTA's JS triggers a blob-download — we still need to consume
      // the `download` event Playwright surfaces, otherwise the blob handle
      // leaks. Catch the timeout silently: the gateway URL may have already
      // fired (`captured` set below) even if Playwright fails to dispatch
      // the synthetic `download` event for the blob.
      const downloadPromise = this.page.waitForEvent('download', { timeout: timeoutMs }).catch(() => undefined)
      await this.page
        .getByRole('link', { name: /^DOWNLOAD FOR/i })
        .first()
        .click({ timeout: 10_000 })
      const dl = await downloadPromise
      if (dl) await dl.cancel().catch(() => {})

      // Poll for the captured URL — page.on('request') may not have fired
      // synchronously with click resolution.
      while (!captured && Date.now() < deadline) {
        await this.page.waitForTimeout(250)
      }
      if (!captured) {
        throw new Error(
          `Post-signup download CTA click did not produce a download-gateway request within ${timeoutMs / 1000}s. ` +
            'Confirm the user is authenticated (anonymous sessions fall back to a generic .dmg with no gateway hit) ' +
            'and that the hero CTA selector `link role / DOWNLOAD FOR` still resolves.'
        )
      }
      return captured
    } finally {
      this.page.off('request', listener)
    }
  }
}
