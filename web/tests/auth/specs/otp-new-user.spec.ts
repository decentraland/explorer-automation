import { test, expect } from '@playwright/test'
import { uniqueUsername } from '../helpers/test-user.js'
import { LandingPage } from '../../landing/pages/LandingPage.js'
import { AuthPage } from '../pages/AuthPage.js'
import { QuickSetupPage } from '../pages/QuickSetupPage.js'
import { generateFreshEmail, waitForOtp } from '../helpers/otp-mailbox.js'

/**
 * New-user signup via the email + OTP path. Mirrors `auth-e2e-tests`'
 * `otp-new-login-setup.spec.ts`. Uses a fresh `+alias` per run, so it
 * always exercises the new-user setup screen. Consumes one OTP per run.
 *
 * The web3 new-user path is in `auth-new-user.spec.ts`. This file exists
 * because the OTP and web3 paths can diverge in the dapp's setup flow
 * (different feature flags, different middlewares); covering both keeps
 * us honest if either side regresses.
 */

test('@web @auth new user can sign up via email + OTP', async ({ page }) => {
  const email = generateFreshEmail()
  const landing = new LandingPage(page)
  const auth = new AuthPage(page)
  const qs = new QuickSetupPage(page)

  await landing.goto()
  await landing.clickSignIn()
  await auth.submitEmail(email)
  await auth.waitForOtpScreen()
  const code = await waitForOtp(email)
  await auth.enterOtp(code)

  await qs.waitFor()
  await qs.fillUsername(uniqueUsername())
  await qs.acceptTerms()
  await qs.submit()
  await qs.clickStartExploring()

  await landing.waitForUrl()
  expect(page.url()).not.toMatch(/\/auth/)
})
