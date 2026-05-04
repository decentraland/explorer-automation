import type { Page } from '@playwright/test';

/**
 * Page Object for `https://decentraland.org/auth/quick-setup`.
 *
 * Shown after a NEW user completes email + OTP. Recurrent users skip this
 * screen entirely. Has username (required), optional email (= newsletter
 * opt-in), terms checkbox (required), and a LET'S GO button. After that,
 * an "Account is Ready!" interstitial shows with a "Start Exploring" CTA.
 */
export class QuickSetupPage {
  constructor(private readonly page: Page) {}

  async waitFor(timeoutMs = 60_000): Promise<void> {
    await this.page.waitForURL(/\/auth\/quick-setup/, { timeout: timeoutMs });
    await this.page.getByRole('textbox', { name: 'Enter your username' }).waitFor({
      state: 'visible',
      timeout: timeoutMs,
    });
  }

  async fillUsername(username: string): Promise<void> {
    await this.page.getByRole('textbox', { name: 'Enter your username' }).fill(username);
  }

  /**
   * Filling this field opts the user in to Decentraland's newsletter
   * (the field's helper text is "Subscribe to Decentraland's newsletter…").
   * For the no-newsletter test, simply skip calling this.
   */
  async subscribeToNewsletter(email: string): Promise<void> {
    await this.page.getByRole('textbox', { name: 'Enter your email' }).fill(email);
  }

  async acceptTerms(): Promise<void> {
    await this.page.getByRole('checkbox', { name: "I agree with Decentraland's" }).check();
  }

  async submit(): Promise<void> {
    await this.page.getByRole('button', { name: "LET'S GO" }).click();
  }

  async clickStartExploring(): Promise<void> {
    await this.page.getByRole('button', { name: 'Start Exploring' }).click();
  }

  // ─── Avatar customization ───────────────────────────────────────────────
  // The bottom-right avatar toolbar exposes:
  //   • RANDOMIZE button — picks a fresh random avatar
  //   • BODY TYPE dropdown — button label reflects the current selection
  //     ("BODY TYPE A" or "BODY TYPE B"). Clicking it opens the option list.

  async clickRandomize(): Promise<void> {
    await this.page.getByRole('button', { name: 'RANDOMIZE' }).click();
  }

  /**
   * Toggles between the two body-type options. Pass 'A' or 'B'.
   *
   * The dropdown trigger is a button whose accessible name is the currently
   * selected option ("BODY TYPE A" or "BODY TYPE B"). After the trigger click,
   * the alternative option is rendered as plain text and matches via
   * `getByText`.
   */
  async selectBodyType(letter: 'A' | 'B'): Promise<void> {
    await this.page.getByRole('button', { name: /BODY TYPE/ }).click();
    await this.page.getByText(`BODY TYPE ${letter}`, { exact: true }).click();
  }
}
