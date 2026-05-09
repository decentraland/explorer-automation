import { test } from '@playwright/test'
import { generatePrivateKey } from 'viem/accounts'
import { randomBytes } from 'node:crypto'
import { walletTest } from '../../../shared/fixtures/wallet-fixture.js'
import { setupMockedWallet, mockNoProfileOnCatalysts } from '../helpers/wallet.js'
import { getBaseUrl } from '../../../shared/helpers/env.js'
import { LandingPage } from '../../landing/pages/LandingPage.js'
import { AuthPage } from '../pages/AuthPage.js'
import { QuickSetupPage } from '../pages/QuickSetupPage.js'
import { getBaseEmail, waitForOtp } from '../helpers/otp-mailbox.js'

/**
 * Recurrent-user login flows. Already-registered accounts skip the
 * `/auth/quick-setup` screen entirely and land directly on the homepage.
 *
 * Two parallel tests, one per auth method:
 *   - web3 wallet — self-bootstraps: generates a fresh key, completes the
 *     new-user flow once (which registers the wallet on prod's catalyst),
 *     then re-logs in with the same key and asserts no quick-setup. No env
 *     var needed.
 *   - email + OTP — uses `IMAP_USER` (must already be a registered
 *     Decentraland account). Only test in the suite that consumes a code.
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
  const email = getBaseEmail()
  const landing = new LandingPage(page)
  const auth = new AuthPage(page)

  await landing.goto()
  await landing.clickSignIn()
  await auth.submitEmail(email)
  await auth.waitForOtpScreen()
  const code = await waitForOtp(email)
  await auth.enterOtp(code)

  await landing.waitForUrl(60_000)
  test.expect(page.url()).not.toMatch(/\/auth\/quick-setup/)
  test.expect(page.url()).not.toMatch(/\/auth\/login/)
})
