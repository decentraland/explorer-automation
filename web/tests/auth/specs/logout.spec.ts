import { generatePrivateKey, privateKeyToAddress } from 'viem/accounts'
import { randomBytes } from 'node:crypto'
import { walletTest as test } from '../../../shared/fixtures/wallet-fixture.js'
import { setupMockedWallet, mockNoProfileOnCatalysts } from '../helpers/wallet.js'
import { buildAuthIdentity, installInjectedWalletMock } from '../../../shared/helpers/auth-identity.js'
import { mockExistingProfile } from '../../../shared/helpers/profile.js'
import { getBaseUrl } from '../../../shared/helpers/env.js'
import { withEnv } from '../../../shared/helpers/url.js'
import { LandingPage } from '../../landing/pages/LandingPage.js'
import { AuthPage } from '../pages/AuthPage.js'
import { QuickSetupPage } from '../pages/QuickSetupPage.js'
import { Navbar } from '../../marketplace/pages/Navbar.js'

/**
 * Verifies the logout UI: register a fresh web3 user via the auth dapp, then
 * drive the marketplace navbar's Log Out entry and assert the SSO identity
 * blob (`single-sign-on-<address>`) is cleared from localStorage.
 *
 * Why we manually seed localStorage after navigation (instead of using
 * `injectAuthIdentity`): `injectAuthIdentity` adds a context-level
 * `addInitScript` that re-seeds the SSO + connect-storage keys on every
 * subsequent navigation. Any nav fired by the logout handler would
 * immediately re-establish the session, making logout untestable. The
 * one-shot `page.evaluate` write happens after navigation and is NOT
 * re-applied on the next nav, so the dapp's logout-driven localStorage
 * clear is final.
 *
 * The recurrent-after-logout re-login phase is out of scope; it's already
 * covered by `recurrent-user.spec.ts`.
 */

const REDIRECT_TO = `${getBaseUrl()}/`
const uniqueUsername = (): string => `QA${randomBytes(3).toString('hex')}`
const SEPOLIA_CHAIN_ID = 11_155_111

const { expect } = test

test('@web @auth logout clears the SSO identity from localStorage', async ({ page, ethereumWalletMock }) => {
  const privateKey = generatePrivateKey()
  const address = privateKeyToAddress(privateKey)

  // Phase 1 — register the wallet via the auth dapp's new-user flow.
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

  // Phase 2 — set up the marketplace-recognizable wallet mocks (init scripts
  // that don't write localStorage), navigate to marketplace, then one-shot
  // seed the SSO + connect keys via page.evaluate. The dapp picks them up
  // on a reload.
  await installInjectedWalletMock(page, privateKey)
  await mockExistingProfile(page, address)
  await page.goto(withEnv(`${getBaseUrl()}/marketplace/`))

  const identity = await buildAuthIdentity(privateKey)
  const ssoKey = `single-sign-on-${address.toLowerCase()}`
  const ssoValue = JSON.stringify({ ...identity, expiration: identity.expiration.toISOString() })
  const connectValue = JSON.stringify({ providerType: 'injected', chainId: SEPOLIA_CHAIN_ID })
  await page.evaluate(
    ({ ssoKey, ssoValue, connectValue }: { ssoKey: string; ssoValue: string; connectValue: string }) => {
      localStorage.setItem(ssoKey, ssoValue)
      localStorage.setItem('decentraland-connect-storage-key', connectValue)
    },
    { ssoKey, ssoValue, connectValue }
  )
  await page.reload({ waitUntil: 'load' })

  const navbar = new Navbar(page)
  await navbar.waitForConnected(60_000)

  // Confirm the SSO blob is present BEFORE logout (sanity check on the seed).
  const before = await page.evaluate(k => localStorage.getItem(k), ssoKey)
  expect(before).not.toBeNull()

  await navbar.clickLogout()

  // The dapp's logout handler clears the SSO blob. Allow a tick for the
  // synchronous teardown to flush.
  await expect.poll(async () => page.evaluate(k => localStorage.getItem(k), ssoKey), { timeout: 10_000 }).toBeNull()
})
