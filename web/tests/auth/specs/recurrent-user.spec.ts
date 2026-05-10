import { test } from '@playwright/test'
import { generatePrivateKey } from 'viem/accounts'
import { randomBytes } from 'node:crypto'
import { walletTest } from '../../../shared/fixtures/wallet-fixture.js'
import { setupMockedWallet, mockNoProfileOnCatalysts } from '../helpers/wallet.js'
import { getBaseUrl } from '../../../shared/helpers/env.js'
import { LandingPage } from '../../landing/pages/LandingPage.js'
import { AuthPage } from '../pages/AuthPage.js'
import { QuickSetupPage } from '../pages/QuickSetupPage.js'
import { generateFreshEmail, waitForOtp } from '../helpers/otp-mailbox.js'

/**
 * Recurrent-user login flows. Already-registered accounts skip the
 * `/auth/quick-setup` screen entirely and land directly on the homepage.
 *
 * Both tests self-bootstrap a fresh user — never reuse a shared registered
 * account — so each run uses a brand-new email/wallet from Thirdweb's POV
 * (its own per-address rate-limit bucket, no stale-state leakage between
 * runs):
 *   - web3 wallet — generates a fresh key, completes the new-user flow once
 *     (registers the wallet on prod's catalyst), then re-logs in with the
 *     same key and asserts no quick-setup.
 *   - email + OTP — generates a fresh `qa-<hash>@<EMAIL_DOMAIN>` address,
 *     signs up via OTP (consuming OTP #1), clears cookies, then signs in
 *     again with the now-registered email (consuming OTP #2) and asserts
 *     no quick-setup.
 */

const REDIRECT_TO = `${getBaseUrl()}/`
const uniqueUsername = (): string => `QA${randomBytes(3).toString('hex')}`

walletTest('@web @auth recurrent user can log in with web3 wallet', async ({ page, ethereumWalletMock }) => {
  const privateKey = generatePrivateKey()

  // Phase 1 — register the wallet via the new-user flow. Forces 404 on the
  // catalyst profile lookup so the dapp routes to quick-setup, where we
  // complete the minimum form (username + ToS) and submit. After Start
  // Exploring lands on home, the wallet has a profile registered on prod.
  const unmockProfile = await mockNoProfileOnCatalysts(page)
  await setupMockedWallet(page, ethereumWalletMock, { privateKey, redirectTo: REDIRECT_TO })
  await new AuthPage(page).clickMetaMaskButton()

  const qs = new QuickSetupPage(page)
  await qs.waitFor()
  await qs.fillUsername(uniqueUsername())
  await qs.acceptTerms()
  await qs.submit()
  await qs.clickStartExploring()
  await new LandingPage(page).waitForUrl()

  // Phase 2 — re-login as a recurrent user. Drop the no-profile mock so the
  // catalyst returns the profile we just created. Same private key, same
  // page → setupMockedWallet is idempotent and re-navigates to /auth/login.
  await unmockProfile()
  await setupMockedWallet(page, ethereumWalletMock, { privateKey, redirectTo: REDIRECT_TO })
  await new AuthPage(page).clickMetaMaskButton()

  await new LandingPage(page).waitForUrl(60_000)
  walletTest.expect(page.url()).not.toMatch(/\/auth\/quick-setup/)
  walletTest.expect(page.url()).not.toMatch(/\/auth\/login/)
})

test('@web @auth recurrent user can log in with email + OTP', async ({ page }) => {
  const email = generateFreshEmail()
  const landing = new LandingPage(page)
  const auth = new AuthPage(page)
  const qs = new QuickSetupPage(page)

  // Phase 1 — sign up a brand-new user via OTP. Each test run uses a fresh
  // email so we never accumulate Thirdweb rate limit on a shared address.
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

  // Phase 2 — re-log in as the now-registered user. Clear cookies AND
  // localStorage / sessionStorage so the dapp treats us as a fresh visitor:
  // decentraland-connect persists the auth identity in localStorage, so
  // clearing only HTTP cookies isn't enough — /auth/login would still see
  // a logged-in user and bounce straight to home.
  await page.context().clearCookies()
  await page.evaluate(() => {
    localStorage.clear()
    sessionStorage.clear()
  })
  await landing.goto()
  await landing.clickSignIn()
  await auth.submitEmail(email)
  await auth.waitForOtpScreen()
  const loginCode = await waitForOtp(email)
  await auth.enterOtp(loginCode)

  await landing.waitForUrl(60_000)
  test.expect(page.url()).not.toMatch(/\/auth\/quick-setup/)
  test.expect(page.url()).not.toMatch(/\/auth\/login/)
})
