import type { Download, Page } from '@playwright/test'
import { getBaseUrl } from '../../../shared/helpers/env.js'

/**
 * Page Object for the Decentraland landing page (`https://decentraland.<tld>`).
 *
 * Same URL renders for both states — pre-login (public hero with Sign In +
 * DOWNLOAD CTA) and post-login (logged-in dashboard). Auth specs use
 * `goto()` / `clickSignIn()` as the entry point and then `waitForUrl()` to
 * confirm we got redirected back to `/` after a successful login.
 */
export class LandingPage {
  constructor(private readonly page: Page) {}

  async goto(): Promise<void> {
    await this.page.goto('/')
  }

  async clickSignIn(): Promise<void> {
    await this.page.getByRole('button', { name: 'Sign In' }).click()
  }

  /**
   * Clicks the "DOWNLOAD FOR <platform>" hero CTA and resolves with the
   * resulting `download` event. The link's accessible name varies by
   * detected OS ("DOWNLOAD FOR macOS" / "Windows" / etc.), but the href
   * always matches `/download_success?os=...` for the platform-aware CTA.
   */
  async downloadLauncher(): Promise<Download> {
    const downloadPromise = this.page.waitForEvent('download', { timeout: 30_000 })
    await this.page.locator('a[href^="/download_success"]').first().click()
    return downloadPromise
  }

  /**
   * Waits for the page URL to match the landing/home URL — the configured
   * dapp host with an optional trailing slash and/or query string. Used
   * post-login to assert the auth flow redirected back to `/`. URL-only
   * check, no DOM — the dapp's logged-in elements use volatile class names
   * with no stable selectors yet.
   *
   * Builds the regex from `getBaseUrl()` so the match works against any
   * configured environment (`decentraland.org`, `decentraland.zone`, etc.).
   */
  async waitForUrl(timeoutMs = 30_000): Promise<void> {
    const host = new URL(getBaseUrl()).host.replace(/\./g, '\\.')
    const re = new RegExp(`${host}\\/?(\\?.*)?$`)
    await this.page.waitForURL(url => re.test(url.toString()), { timeout: timeoutMs })
  }
}
