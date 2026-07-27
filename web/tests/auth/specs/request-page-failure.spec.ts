import { generatePrivateKey, privateKeyToAccount } from 'viem/accounts'
import { uniqueUsername } from '../helpers/test-user.js'
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
import { getEphemeralMessage } from '../../../shared/helpers/identity.js'

/**
 * Negative path for the RequestPage flow — companion to
 * `request-page.spec.ts`'s happy path. Targets the auth site ↔ auth-api
 * contract; Explorer is not involved.
 *
 * decentraland/auth#437 retired the `dcl_personal_sign` sign-in in auth-site
 * 5.0.0. A client that hasn't migrated to the identity handoff still sends it,
 * and auth-api still mints the request — so the rejection has to happen on the
 * auth site, and it has to be a dead end. The generic recover error won't do:
 * its "Try Again" re-opens the client, which re-creates the same rejected
 * request, looping the user with no way out. Instead the user is told to
 * update their app, with no retry offered.
 *
 * Needs auth-site >= 5.0.0 on the host under test — earlier builds still
 * render the VerifySignIn approve/deny screen this test exists to prove is
 * gone. Validate against `.zone` (`WEB_BASE_URL`) while the release rolls out.
 */

const REDIRECT_TO = `${getBaseUrl()}/`

const { expect } = test

test('@web @auth RequestPage retired sign-in (dcl_personal_sign) tells the user to update', async ({
  page,
  ethereumWalletMock
}) => {
  const privateKey = generatePrivateKey()
  const account = privateKeyToAccount(privateKey)

  // Register a DCL profile for our wallet so the RequestPage probe doesn't
  // bounce the dapp back to /auth/login before it can reject the method.
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

  // Exactly what an unmigrated Explorer sends: the retired method carrying an
  // identity-authorization payload. Two guards would reject this, and the
  // allowlist check runs first — that ordering is what routes it to the
  // "update your app" view instead of the generic impersonation error. If this
  // ever lands on a different view, check that ordering before the testid.
  const ephemeralAccount = privateKeyToAccount(generatePrivateKey())
  const expiration = new Date(Date.now() + 24 * 60 * 60 * 1000)
  const ephemeralMessage = getEphemeralMessage(ephemeralAccount.address, expiration)
  const { requestId } = await createAuthRequest('dcl_personal_sign', [ephemeralMessage])
  expect(requestId).toBeTruthy()

  await installAutoWalletMockInitScript(page, account.address)
  await page.goto(`/auth/requests/${requestId}`, { waitUntil: 'load' })
  await applyPersonalSignOverride(page)

  await page.locator('[data-testid="outdated-client-error"]').waitFor({ state: 'visible', timeout: 30_000 })

  // Terminal by design — no retry affordance, so the user can't re-enter the
  // loop this view exists to break.
  await expect(page.locator('[data-testid="client-login-error-try-again-button"]')).toBeHidden()

  // Nothing reached the wallet, so the request must stay unfulfilled —
  // auth-api keeps answering 204 and the poll runs out its deadline.
  await expect(pollAuthOutcome(requestId, 10_000)).rejects.toThrow(/timed out/i)
})
