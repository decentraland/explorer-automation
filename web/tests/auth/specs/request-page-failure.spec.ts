import { generatePrivateKey, privateKeyToAccount } from 'viem/accounts'
import { randomBytes } from 'node:crypto'
import { walletTest as test } from '../../../shared/fixtures/wallet-fixture.js'
import {
  setupMockedWallet,
  mockNoProfileOnCatalysts,
  installAutoWalletMockInitScript,
  applyPersonalSignOverride
} from '../helpers/wallet.js'
import { getBaseUrl } from '../../../shared/helpers/env.js'
import { LandingPage } from '../../landing/pages/LandingPage.js'
import { AuthPage } from '../pages/AuthPage.js'
import { QuickSetupPage } from '../pages/QuickSetupPage.js'
import { createAuthRequest, pollAuthOutcome } from '../helpers/auth-server.js'

/**
 * Negative path for the RequestPage flow — companion to
 * `request-page.spec.ts`'s happy paths. Targets the auth site ↔ auth-api
 * contract; Explorer is not involved.
 *
 *   - decline: user denies the request; auth-api returns an outcome with
 *     no successful `result` (typically an `error` field).
 */

const REDIRECT_TO = `${getBaseUrl()}/`
const uniqueUsername = (): string => `QA${randomBytes(3).toString('hex')}`

const { expect } = test

test('@web @auth RequestPage login (dcl_personal_sign) — decline', async ({ page, ethereumWalletMock }) => {
  const privateKey = generatePrivateKey()
  const account = privateKeyToAccount(privateKey)

  // Register a DCL profile for our wallet so the RequestPage probe doesn't
  // bounce the dapp back to /auth/login.
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
  await unmockProfile()

  const { requestId } = await createAuthRequest('dcl_personal_sign', ['Hello, world!'])
  expect(requestId).toBeTruthy()

  await installAutoWalletMockInitScript(page, account.address)
  await page.goto(`/auth/requests/${requestId}`, { waitUntil: 'load' })
  await applyPersonalSignOverride(page)

  // Deny button. Testid mirrors the approve testid from request-page.spec.ts.
  // Verify against the running site if the dapp uses a different name.
  const denyBtn = page.locator(
    '[data-testid="verify-sign-in-deny-button"], [data-testid="verify-sign-in-cancel-button"], [data-testid="verify-sign-in-reject-button"]'
  )
  await denyBtn.first().waitFor({ state: 'visible', timeout: 30_000 })
  await denyBtn.first().click()

  const outcome = await pollAuthOutcome(requestId, 30_000)
  expect(outcome.sender.toLowerCase()).toBe(account.address.toLowerCase())
  // Decline produces no signature. `result` should be absent (the auth-api
  // returns an `error` field instead).
  expect(outcome.result).toBeFalsy()
})

