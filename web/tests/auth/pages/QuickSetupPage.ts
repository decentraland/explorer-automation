import type { Page } from '@playwright/test'

/**
 * Timeout budget for the two slow spots on this screen (measured on prod,
 * July 2026 — see the method docs for the mechanics):
 *   - ToS checkbox `check()` — up to ~20s while the avatar preview loads.
 *   - "Start Exploring" CTA — appears 5-45s+ after LET'S GO (profile deploy).
 * Kept here as the single place to tune if prod latency shifts.
 */
const TOS_CHECKBOX_TIMEOUT_MS = 60_000
const START_EXPLORING_TIMEOUT_MS = 120_000

/**
 * Page Object for `https://decentraland.org/auth/quick-setup`.
 *
 * Shown after a NEW user completes email + OTP or a web3 signup. Recurrent
 * users skip this screen entirely. Has username (required), a newsletter
 * opt-in, terms checkbox (required), and a LET'S GO button. After that,
 * an "Account is Ready!" interstitial shows with a "Start Exploring" CTA.
 *
 * The newsletter opt-in renders differently per signup method (observed on
 * prod, July 2026):
 *   - web3 signup — an optional "Enter your email" textbox (filling it
 *     subscribes the user). Covered by `subscribeToNewsletter`.
 *   - email + OTP signup — the dapp already knows the email, so it shows a
 *     "Subscribe to newsletter…" checkbox instead; there is no email textbox.
 *     No spec currently opts in on this path — add a checkbox helper if one
 *     ever needs to.
 */
export class QuickSetupPage {
  constructor(private readonly page: Page) {}

  async waitFor(timeoutMs = 60_000): Promise<void> {
    await this.page.waitForURL(/\/auth\/quick-setup/, { timeout: timeoutMs })
    await this.page.getByRole('textbox', { name: 'Enter your username' }).waitFor({
      state: 'visible',
      timeout: timeoutMs
    })
  }

  async fillUsername(username: string): Promise<void> {
    await this.page.getByRole('textbox', { name: 'Enter your username' }).fill(username)
  }

  /**
   * Filling this field opts the user in to Decentraland's newsletter
   * (the field's helper text is "Subscribe to Decentraland's newsletter…").
   * For the no-newsletter test, simply skip calling this.
   *
   * web3 signup variant only — the OTP variant renders a checkbox instead
   * of this textbox (see class doc).
   */
  async subscribeToNewsletter(email: string): Promise<void> {
    await this.page.getByRole('textbox', { name: 'Enter your email' }).fill(email)
  }

  /**
   * The ToS checkbox is a MUI input under a custom icon; while the avatar
   * preview iframe is still loading its assets the row keeps re-rendering
   * and `check()`'s actionability wait can legitimately take ~20s (measured
   * on prod). Bound it explicitly so a genuinely wedged page fails here
   * with a clear message instead of eating the whole test budget.
   */
  async acceptTerms(timeoutMs = TOS_CHECKBOX_TIMEOUT_MS): Promise<void> {
    await this.page.getByRole('checkbox', { name: "I agree with Decentraland's" }).check({ timeout: timeoutMs })
  }

  async submit(): Promise<void> {
    await this.page.getByRole('button', { name: "LET'S GO" }).click()
  }

  /**
   * After LET'S GO the dapp deploys the profile entity to a catalyst and the
   * button reads "DEPLOYING..." until the "Account is Ready!" interstitial
   * appears. Deploy-to-interstitial latency is 5-45s+ on prod (catalyst-side,
   * high variance; measured July 2026), so wait for the CTA explicitly with
   * a budget that absorbs the worst observed case before clicking.
   */
  async clickStartExploring(timeoutMs = START_EXPLORING_TIMEOUT_MS): Promise<void> {
    const cta = this.page.getByRole('button', { name: 'Start Exploring' })
    await cta.waitFor({ state: 'visible', timeout: timeoutMs })
    await cta.click()
  }

  // ─── Avatar customization ───────────────────────────────────────────────
  // The bottom-right avatar toolbar exposes:
  //   • RANDOMIZE button — picks a fresh random avatar
  //   • BODY TYPE dropdown — button label reflects the current selection
  //     ("BODY TYPE A" or "BODY TYPE B"). Clicking it opens the option list.

  async clickRandomize(): Promise<void> {
    await this.page.getByRole('button', { name: 'RANDOMIZE' }).click()
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
    await this.page.getByRole('button', { name: /BODY TYPE/ }).click()
    await this.page.getByText(`BODY TYPE ${letter}`, { exact: true }).click()
  }
}
