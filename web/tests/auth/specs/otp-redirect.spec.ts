import { test, expect } from '@playwright/test'
import { randomBytes } from 'node:crypto'
import { LandingPage } from '../../landing/pages/LandingPage.js'
import { AuthPage } from '../pages/AuthPage.js'
import { QuickSetupPage } from '../pages/QuickSetupPage.js'
import { generateFreshEmail, waitForOtp } from '../helpers/otp-mailbox.js'
import { getBaseUrl } from '../../../shared/helpers/env.js'

/**
 * Verifies the dapp respects the `redirectTo` query param for the OTP path:
 * a recurrent email + OTP login with `redirectTo=<marketplace-url>` should
 * land on `/marketplace`, not on the homepage. Parallel of
 * `web3-redirect.spec.ts` for the OTP login.
 *
 * Self-bootstraps a fresh user (consumes OTP #1) so phase 2 is a true
 * recurrent login (consumes OTP #2). Each run uses a brand-new
 * `qa-<hash>@<EMAIL_DOMAIN>` so it has its own Thirdweb rate-limit bucket.
 *
 * Phase 2 navigates directly to `/auth/login?redirectTo=<url>` because the
 * landing-page sign-in entry doesn't wire redirectTo for OTP — only the
 * dapps that bounce users to auth do.
 */

const REDIRECT_TARGET = `${getBaseUrl()}/marketplace`
const uniqueUsername = (): string => `QA${randomBytes(3).toString('hex')}`

test('@web @auth OTP login respects redirectTo (lands on /marketplace)', async ({ page }) => {
  const email = generateFreshEmail()
  const landing = new LandingPage(page)
  const auth = new AuthPage(page)
  const qs = new QuickSetupPage(page)

  // Phase 1 — sign up a brand-new user via OTP so phase 2 is a true recurrent
  // login. No redirectTo needed here; we just want the profile registered.
  await landing.goto()
  await landing.clickSignIn()
  await auth.submitEmail(email)
  await auth.waitForOtpScreen()
  const signupCode = await waitForOtp(email)
  await auth.enterOtp(signupCode)

  await qs.waitFor()
  await qs.fillUsername(uniqueUsername())
  await qs.acceptTerms()
  await qs.submit()
  await qs.clickStartExploring()
  await landing.waitForUrl()

  // Phase 2 — clear cookies AND localStorage / sessionStorage (decentraland-connect
  // persists the identity in localStorage, so clearing only HTTP cookies isn't
  // enough — /auth/login would still see a logged-in user and bounce to home).
  await page.context().clearCookies()
  await page.evaluate(() => {
    localStorage.clear()
    sessionStorage.clear()
  })

  // Drive a recurrent OTP login with the marketplace as the redirectTo target.
  await page.goto(`/auth/login?redirectTo=${encodeURIComponent(REDIRECT_TARGET)}`)
  await auth.submitEmail(email)
  await auth.waitForOtpScreen()
  const loginCode = await waitForOtp(email)
  await auth.enterOtp(loginCode)

  await page.waitForURL(/\/marketplace/, { timeout: 60_000 })
  expect(page.url()).toContain('/marketplace')
  expect(page.url()).not.toMatch(/\/auth\/(login|quick-setup)/)
})
