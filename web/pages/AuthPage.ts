import type { Page } from '@playwright/test';

/**
 * Page Object for `https://decentraland.org/auth/login` — email + OTP login.
 *
 * The OTP screen renders six single-character boxes (`otp-input-0`..`otp-input-5`)
 * that auto-advance on keydown and auto-submit once the sixth digit is entered.
 * Programmatic `fill()` doesn't fire keydown so it doesn't advance — we type via
 * `page.keyboard.press(digit)` instead.
 */
export class AuthPage {
  constructor(private readonly page: Page) {}

  async goto(): Promise<void> {
    await this.page.goto('/auth');
  }

  async submitEmail(address: string): Promise<void> {
    await this.page.getByTestId('email-input').fill(address);
    await this.page.getByTestId('email-submit-button').click();
  }

  /**
   * Clicks the MetaMask wallet-connect button. The auth screen exposes
   * `data-testid="metamask-button"` on a button whose label varies; matching
   * via `*=` keeps the locator resilient to label changes.
   */
  async clickMetaMaskButton(): Promise<void> {
    const btn = this.page.locator('[data-testid*="metamask-button"]');
    await btn.waitFor({ state: 'visible', timeout: 30_000 });
    await btn.click();
  }

  async waitForOtpScreen(timeoutMs = 30_000): Promise<void> {
    await this.page.getByTestId('otp-input-0').waitFor({ state: 'visible', timeout: timeoutMs });
  }

  async enterOtp(code: string): Promise<void> {
    if (code.length !== 6) throw new Error(`OTP must be 6 digits, got "${code}"`);
    await this.page.getByTestId('otp-input-0').click();
    for (const digit of code) {
      await this.page.keyboard.press(digit);
      await this.page.waitForTimeout(50);
    }
  }
}
