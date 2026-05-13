import { test, expect } from '@playwright/test'
import { LandingPage } from '../pages/LandingPage.js'

/**
 * The "DOWNLOAD FOR macOS" CTA is rendered on the public landing hero —
 * no authentication required. Success = the browser fires a `download` event
 * with a non-empty filename, which means the launcher .dmg started downloading.
 *
 * Tagged `@landing` (not `@download`) because it's a member of the landing-
 * surface bucket. Future landing specs (navbar, search, etc.) get the same
 * `@landing` sub-tag so the workflow's `landing` selector keeps working.
 */
test.describe('@web @landing launcher download', () => {
  // The dapp's hero picks the OS-specific download CTA from `navigator.userAgent`.
  // On a Linux CI runner the rendered href becomes `/download_success?os=Linux`,
  // whose thank-you page doesn't trigger a file download (no Linux launcher
  // binary) — `waitForEvent('download')` then times out. Lock the UA to macOS
  // so the test asserts the same `.dmg` flow regardless of runner OS.
  test.use({
    userAgent:
      'Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36'
  })

  test('clicking "DOWNLOAD FOR macOS" starts a download', async ({ page }) => {
    const landing = new LandingPage(page)

    await landing.goto()
    const download = await landing.downloadLauncher()

    expect(download.suggestedFilename().length).toBeGreaterThan(0)
  })
})
