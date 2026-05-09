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
  test('clicking "DOWNLOAD FOR macOS" starts a download', async ({ page }) => {
    const landing = new LandingPage(page)

    await landing.goto()
    const download = await landing.downloadLauncher()

    expect(download.suggestedFilename().length).toBeGreaterThan(0)
  })
})
