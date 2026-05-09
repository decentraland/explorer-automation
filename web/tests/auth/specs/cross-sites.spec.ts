import { generatePrivateKey } from 'viem/accounts'
import { randomBytes } from 'node:crypto'
import { walletTest as test } from '../../../shared/fixtures/wallet-fixture.js'
import { setupMockedWallet, rebindWalletMock, mockNoProfileOnCatalysts } from '../helpers/wallet.js'
import { getBaseUrl } from '../../../shared/helpers/env.js'
import { AuthPage } from '../pages/AuthPage.js'
import { QuickSetupPage } from '../pages/QuickSetupPage.js'
import { HomePage } from '../../landing/pages/HomePage.js'

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
  const username = `QA${randomBytes(3).toString('hex')}`

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
  await new HomePage(page).waitFor()

  // Phase 2 — drop the no-profile mock so subdomain catalyst lookups succeed,
  // then walk through each site and assert no bounce-to-/auth happens.
  await unmockProfile()

  for (const site of SITES) {
    await page.goto(`/${site}`, { waitUntil: 'load' })
    // Re-bind Web3Mock on the new page state so any wallet-touching code on
    // the subdomain gets our address back, not the mock's default.
    await rebindWalletMock(page, ethereumWalletMock, privateKey)
    await page.waitForTimeout(3_000)
    expect(page.url(), `expected to land on /${site}`).toContain(`/${site}`)
    expect(page.url(), `expected to NOT bounce to /auth on ${site}`).not.toMatch(/\/auth\/login/)
  }
})
