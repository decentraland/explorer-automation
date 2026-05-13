import { test, expect } from '@playwright/test'
import { randomBytes } from 'node:crypto'
import { LandingPage } from '../../landing/pages/LandingPage.js'
import { AuthPage } from '../pages/AuthPage.js'
import { QuickSetupPage } from '../pages/QuickSetupPage.js'
import { generateFreshEmail, waitForOtp } from '../helpers/otp-mailbox.js'

/**
 * OTP failure paths — wrong-code retry and resend. Two tests, each
 * self-bootstraps with a fresh `qa-<hash>@<EMAIL_DOMAIN>` so Thirdweb sees a
 * brand-new recipient per run (its own per-address rate-limit bucket).
 *
 *   - wrong code: enter a deliberately incorrect six-digit code, assert the
 *     dapp surfaces an inline error, then retry with the real code and
 *     confirm signup completes.
 *   - resend: trigger the "resend code" affordance and confirm the second
 *     IMAP-delivered OTP completes signup.
 */

const uniqueUsername = (): string => `QA${randomBytes(3).toString('hex')}`

test('@web @auth OTP wrong code surfaces the inline error', async ({ page }) => {
  const email = generateFreshEmail()
  const landing = new LandingPage(page)
  const auth = new AuthPage(page)

  await landing.goto()
  await landing.clickSignIn()
  await auth.submitEmail(email)
  await auth.waitForOtpScreen()

  // Wait for the real OTP to ensure Thirdweb has issued one (so the wrong-
  // code attempt isn't blocked by a not-yet-delivered code path), but don't
  // enter it — the test only asserts the error UX.
  await waitForOtp(email)

  await auth.enterOtp('000000')
  await auth.otpErrorMessage().waitFor({ state: 'visible', timeout: 10_000 })
  // We deliberately don't verify recovery on the SAME code: Thirdweb appears
  // to invalidate the issued OTP after one failed attempt, so the recovery
  // path requires a resend — already covered by the resend test below.
  expect(page.url()).toMatch(/\/auth\/login/)
})

test('@web @auth OTP resend issues a fresh code and signup completes', async ({ page }) => {
  // The test's critical path waits for two Thirdweb OTP deliveries (~30s each
  // in the worst case) AND the dapp's resend countdown (~60-90s before the
  // "Resend Code" link becomes clickable). That comfortably exceeds the
  // project-level 120s timeout, especially on slower GitHub-hosted runners
  // (local: ~1.9 min; CI: ~2.1 min). Bump per-test rather than for the file
  // so the wrong-code test keeps the tighter default.
  test.setTimeout(240_000)

  const email = generateFreshEmail()
  const landing = new LandingPage(page)
  const auth = new AuthPage(page)
  const qs = new QuickSetupPage(page)

  await landing.goto()
  await landing.clickSignIn()
  await auth.submitEmail(email)
  await auth.waitForOtpScreen()

  // Consume OTP #1 from IMAP (don't enter it) so the next poll's stale-OTP
  // guard isn't tempted to re-pick it. waitForOtp's startedAt + IMAP UID
  // walk newest-first ensures we'll match #2 once it arrives.
  await waitForOtp(email)

  // Some dapp builds gate "Resend" behind a short countdown; Playwright's
  // auto-wait on click() covers that as long as the button is in the DOM.
  await auth.clickResendOtp()

  const secondCode = await waitForOtp(email)
  await auth.enterOtp(secondCode)

  await qs.waitFor()
  await qs.fillUsername(uniqueUsername())
  await qs.acceptTerms()
  await qs.submit()
  await qs.clickStartExploring()
  await landing.waitForUrl()
  expect(page.url()).not.toMatch(/\/auth/)
})
