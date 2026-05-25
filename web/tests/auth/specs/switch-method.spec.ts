import { generatePrivateKey } from 'viem/accounts'
import { uniqueUsername } from '../helpers/test-user.js'
import { walletTest as test } from '../../../shared/fixtures/wallet-fixture.js'
import { setupMockedWallet, mockNoProfileOnCatalysts } from '../helpers/wallet.js'
import { getBaseUrl } from '../../../shared/helpers/env.js'
import { LandingPage } from '../../landing/pages/LandingPage.js'
import { AuthPage } from '../pages/AuthPage.js'
import { QuickSetupPage } from '../pages/QuickSetupPage.js'
import { generateFreshEmail, waitForOtp } from '../helpers/otp-mailbox.js'

/**
 * Switches authentication method mid-session: signs up first via OTP, then
 * (in a fresh page within the same browser context) logs in via web3 wallet
 * — verifying both flows work back-to-back without any cross-contamination
 * of session state.
 *
 * Mirrors `auth-e2e-tests`' `switch-otp-to-web3-wallet-setup.spec.ts` (the
 * non-WebGL variant). Consumes one OTP per run.
 */

const REDIRECT_TO = `${getBaseUrl()}/`

const { expect } = test

test('@web @auth user can switch from OTP to web3 wallet sign-up', async ({ page, context, ethereumWalletMock }) => {
  // Phase 1 — sign up a fresh user via OTP.
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

  // Phase 2 — open a fresh page in the SAME context and sign up via web3.
  // The wallet mock fixture is bound to `page`; rebind it to the new page.
  const page2 = await context.newPage()
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  ;(ethereumWalletMock as any).page = page2

  const privateKey = generatePrivateKey()
  await mockNoProfileOnCatalysts(page2)
  await setupMockedWallet(page2, ethereumWalletMock, { privateKey, redirectTo: REDIRECT_TO })
  await new AuthPage(page2).clickMetaMaskButton()

  const qs2 = new QuickSetupPage(page2)
  await qs2.waitFor()
  await qs2.fillUsername(uniqueUsername())
  await qs2.acceptTerms()
  await qs2.submit()
  await qs2.clickStartExploring()

  await new LandingPage(page2).waitForUrl()
  expect(page2.url()).not.toMatch(/\/auth/)

  await page2.close()
})
