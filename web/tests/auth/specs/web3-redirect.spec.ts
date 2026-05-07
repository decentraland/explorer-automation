import { generatePrivateKey } from 'viem/accounts'
import { randomBytes } from 'node:crypto'
import { walletTest as test } from '../../../shared/fixtures/wallet-fixture.js'
import { setupMockedWallet, mockNoProfileOnCatalysts } from '../helpers/wallet.js'
import { AuthPage } from '../pages/AuthPage.js'
import { QuickSetupPage } from '../pages/QuickSetupPage.js'
import { HomePage } from '../pages/HomePage.js'

/**
 * Verifies the dapp respects the `redirectTo` query param: a recurrent web3
 * login with `redirectTo=https://decentraland.org/marketplace` should land on
 * `/marketplace`, not on the homepage. This is the path other DCL dapps and
 * the launcher use to bring users back to where they started after auth.
 *
 * Mirrors `auth-e2e-tests`' `web3-login-redirect-to-auth.spec.ts` but redirects
 * to `/marketplace` instead of `/account`. Self-bootstraps a fresh wallet via
 * the new-user flow so it doesn't depend on any pre-registered account.
 *
 * OTP variant intentionally skipped — it would consume an extra Thirdweb code
 * for a pretty narrow assertion (URL routing of an existing account).
 */

const REDIRECT_TARGET = 'https://decentraland.org/marketplace'
const uniqueUsername = (): string => `QA${randomBytes(3).toString('hex')}`

const { expect } = test

test('@web web3 login respects redirectTo (lands on /marketplace)', async ({ page, ethereumWalletMock }) => {
  const privateKey = generatePrivateKey()

  // Phase 1 — register the wallet (so phase 2 is a true recurrent login).
  const unmockProfile = await mockNoProfileOnCatalysts(page)
  await setupMockedWallet(page, ethereumWalletMock, {
    privateKey,
    redirectTo: 'https://decentraland.org/'
  })
  await new AuthPage(page).clickMetaMaskButton()

  const qs = new QuickSetupPage(page)
  await qs.waitFor()
  await qs.fillUsername(uniqueUsername())
  await qs.acceptTerms()
  await qs.submit()
  await qs.clickStartExploring()
  await new HomePage(page).waitFor()

  // Phase 2 — recurrent login with the marketplace as the redirectTo target.
  await unmockProfile()
  await setupMockedWallet(page, ethereumWalletMock, {
    privateKey,
    redirectTo: REDIRECT_TARGET
  })
  await new AuthPage(page).clickMetaMaskButton()

  await page.waitForURL(/\/marketplace/, { timeout: 60_000 })
  expect(page.url()).toContain('/marketplace')
  expect(page.url()).not.toMatch(/\/auth\/(login|quick-setup)/)
})
