import type { Page } from '@playwright/test'

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
    await this.page.goto('/auth')
  }

  async submitEmail(address: string): Promise<void> {
    await this.page.getByTestId('email-input').fill(address)
    await this.page.getByTestId('email-submit-button').click()
  }

  /**
   * Clicks the MetaMask wallet-connect button. The auth screen exposes
   * `data-testid="metamask-button"` on a button whose label varies; matching
   * via `*=` keeps the locator resilient to label changes.
   */
  async clickMetaMaskButton(): Promise<void> {
    const btn = this.page.locator('[data-testid*="metamask-button"]')
    await btn.waitFor({ state: 'visible', timeout: 30_000 })
    await btn.click()
  }

  /**
   * Wait for the dapp to transition into the OTP entry screen.
   *
   * Races the `otp-input-0` testid against the inline "Rate limit exceeded"
   * error on the email screen — Thirdweb's per-IP OTP-send bucket is the
   * common cause of this method timing out. When it fires, throw with a
   * descriptive message so the failure is immediate and obvious in CI logs
   * rather than a generic 30s `waitFor` timeout.
   *
   * Thirdweb does not expose remaining quota via API; the only signal is the
   * UI text. Wait ~10-60 minutes before retrying, or run from a different
   * egress IP / Thirdweb project.
   */
  async waitForOtpScreen(timeoutMs = 30_000): Promise<void> {
    const otpInput = this.page.getByTestId('otp-input-0')
    const rateLimitError = this.page.getByText(/rate limit exceeded/i)
    await otpInput.or(rateLimitError).waitFor({ state: 'visible', timeout: timeoutMs })
    if (await rateLimitError.isVisible()) {
      throw new Error(
        'Thirdweb OTP rate limit exceeded (per-IP-per-hour bucket). ' +
          'Wait ~10-60 minutes before retrying OTP specs, or run from a different egress IP. ' +
          'TEST_EMAIL_PREFIX does not bypass this limit — each unique address only side-steps the per-email bucket.'
      )
    }
  }

  async enterOtp(code: string): Promise<void> {
    if (code.length !== 6) throw new Error(`OTP must be 6 digits, got "${code}"`)
    await this.page.getByTestId('otp-input-0').click()
    for (const digit of code) {
      await this.page.keyboard.press(digit)
      await this.page.waitForTimeout(50)
    }
  }

  /**
   * Inline error shown after the dapp rejects a wrong OTP code. Rendered as
   * plain styled text (no `role="alert"`), so match the wording directly.
   * Exact text observed on prod: "Failed to verify the code. Please check
   * and try again."
   */
  otpErrorMessage(): import('@playwright/test').Locator {
    return this.page.getByText(/failed to verify the code/i)
  }

  /**
   * "Resend Code" affordance on the OTP screen. Rendered as a styled link
   * (not a button or anchor) and gated by a ~60-90s countdown — during the
   * countdown it reads "Resend Code (1:13)" and is unclickable. Once the
   * timer expires the label flips to plain "Resend Code". Match the exact
   * post-countdown label so the wait naturally blocks until it's enabled.
   */
  async clickResendOtp(timeoutMs = 120_000): Promise<void> {
    const link = this.page.getByText(/^resend code$/i)
    await link.waitFor({ state: 'visible', timeout: timeoutMs })
    await link.click()
  }
}
