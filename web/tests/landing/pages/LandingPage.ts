import type { Download, Page } from '@playwright/test'

/**
 * Page Object for the Decentraland landing page (`https://decentraland.org`).
 * Logged-out visitors see a "Sign In" button that takes them to `/auth`, plus
 * the "DOWNLOAD FOR <platform>" hero CTA.
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
}
