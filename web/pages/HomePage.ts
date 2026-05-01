import type { Page, Download } from '@playwright/test';

/**
 * Page Object for the post-login Decentraland homepage (`https://decentraland.org/`).
 * The hero exposes platform-specific download links plus the navbar.
 */
export class HomePage {
  constructor(private readonly page: Page) {}

  async waitFor(timeoutMs = 30_000): Promise<void> {
    await this.page.waitForURL((url) => /decentraland\.org\/?(\?.*)?$/.test(url.toString()), {
      timeout: timeoutMs,
    });
  }

  /**
   * Clicks the "DOWNLOAD FOR <platform>" hero CTA and resolves with the
   * resulting `download` event. The link's accessible name varies by
   * detected OS ("DOWNLOAD FOR macOS" / "Windows" / etc.), but the href
   * always matches `/download_success?os=...` for the platform-aware CTA.
   */
  async downloadLauncher(): Promise<Download> {
    const downloadPromise = this.page.waitForEvent('download', { timeout: 30_000 });
    await this.page.locator('a[href^="/download_success"]').first().click();
    return downloadPromise;
  }
}
