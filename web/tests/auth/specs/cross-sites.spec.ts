import { generatePrivateKey } from 'viem/accounts'
import { uniqueUsername } from '../helpers/test-user.js'
import { walletTest as test } from '../../../shared/fixtures/wallet-fixture.js'
import { setupMockedWallet, rebindWalletMock, mockNoProfileOnCatalysts } from '../helpers/wallet.js'
import { getBaseUrl } from '../../../shared/helpers/env.js'
import { LandingPage } from '../../landing/pages/LandingPage.js'
import { AuthPage } from '../pages/AuthPage.js'
import { QuickSetupPage } from '../pages/QuickSetupPage.js'

/**
 * Verifies that a web3 session established on `decentraland.org/auth` carries
 * across the dapp's sub-routes (`/marketplace`, `/builder`, `/account`). The
 * subdomains share the same origin so cookies/localStorage propagate; the
 * test guards against a future regression where one of those routes loses
 * the session and bounces the user back to `/auth`.
 *
 * Mirrors `auth-e2e-tests`' `web3-logged-in-across-sites.spec.ts`. Like
 * theirs, we only assert the URL stays on the target site (no logged-in
 * indicator is exposed via stable selectors yet).
 */

const REDIRECT_TO = `${getBaseUrl()}/`
const SITES = ['marketplace', 'builder', 'account'] as const

const { expect } = test

test('@web @auth web3 session persists across marketplace, builder, and account', async ({
  page,
  ethereumWalletMock
}) => {
  const privateKey = generatePrivateKey()
  const username = uniqueUsername()

  // Phase 1 — register the wallet and reach the homepage.
  const unmockProfile = await mockNoProfileOnCatalysts(page)
  await setupMockedWallet(page, ethereumWalletMock, { privateKey, redirectTo: REDIRECT_TO })
  await new AuthPage(page).clickMetaMaskButton()

  const qs = new QuickSetupPage(page)
  await qs.waitFor()
  await qs.fillUsername(username)
  await qs.acceptTerms()
  await qs.submit()
  await qs.clickStartExploring()
  await new LandingPage(page).waitForUrl()

  // Phase 2 — drop the no-profile mock so subdomain catalyst lookups succeed,
  // then walk through each site and assert no bounce-to-/auth happens.
  await unmockProfile()

  for (const site of SITES) {
    await page.goto(`/${site}`, { waitUntil: 'load' })
    // Re-bind Web3Mock on the new page state so any wallet-touching code on
    // the subdomain gets our address back, not the mock's default.
    await rebindWalletMock(page, ethereumWalletMock, privateKey)

    // A lost session bounces the user to /auth/login via a client-side redirect
    // shortly after load. Actively watch for that navigation for a bounded window
    // instead of sleeping a fixed 3s and snapshotting the URL once — the old
    // approach silently passed a bounce that fired just after the snapshot.
    // waitForURL resolves the instant a bounce happens (at any point in the
    // window) and rejects on timeout when the session held.
    const bounced = await page
      .waitForURL(/\/auth\/login/, { timeout: 5_000 })
      .then(() => true)
      .catch(() => false)
    expect(bounced, `expected to NOT bounce to /auth/login on ${site}`).toBe(false)
    expect(page.url(), `expected to stay on /${site}`).toContain(`/${site}`)
  }
})
