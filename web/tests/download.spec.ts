import { test, expect } from '@playwright/test';
import { LandingPage } from '../pages/LandingPage.js';
import { HomePage } from '../pages/HomePage.js';

/**
 * The "DOWNLOAD FOR macOS" CTA is rendered on the public landing hero —
 * no authentication required. Success = the browser fires a `download` event
 * with a non-empty filename, which means the launcher .dmg started downloading.
 */
test.describe('@web launcher download', () => {
  test('clicking "DOWNLOAD FOR macOS" starts a download', async ({ page }) => {
    const landing = new LandingPage(page);
    const home = new HomePage(page);

    await landing.goto();
    const download = await home.downloadLauncher();

    expect(download.suggestedFilename().length).toBeGreaterThan(0);
  });
});
