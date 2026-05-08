import { test as base, type Fixtures, type PlaywrightTestArgs, type PlaywrightTestOptions } from '@playwright/test'
import { walletTest as baseWalletTest } from '../../../shared/fixtures/wallet-fixture.js'
import { setupTestWallet } from '../helpers/wallet-setup.js'
import { setupWalletPool, type WalletPool } from '../helpers/wallet-pool.js'
import { AccountPage } from '../pages/AccountPage.js'
import { AssetPage } from '../pages/AssetPage.js'
import { AuthorizationModal } from '../pages/AuthorizationModal.js'
import { BrowsePage } from '../pages/BrowsePage.js'
import { BuyWithCryptoModal } from '../pages/BuyWithCryptoModal.js'
import { Navbar } from '../pages/Navbar.js'
import { SellModal } from '../pages/SellModal.js'
import { SignInPage } from '../pages/SignInPage.js'

/**
 * Marketplace test fixtures. Two flavors:
 *
 *  - `marketplaceTest`  — POMs only, no wallet. Use for public/unauthenticated
 *                         specs (browse, etc.). Synpress's wallet mock is *not*
 *                         installed; without seeded accounts it makes the
 *                         marketplace's catalog calls fail on initial load.
 *  - `walletTest`       — POMs + Synpress wallet mock. Use for any spec that
 *                         calls `injectAuthIdentity` / `setupBroadcastWallet`.
 *
 * Both expose the same page-object names so a spec can switch between them
 * without further changes if its auth requirements change.
 *
 * Page objects don't pre-fetch DOM (Playwright Locators are lazy), so creating
 * them per-test is free.
 */
export type MarketplacePages = {
  account: AccountPage
  asset: AssetPage
  authModal: AuthorizationModal
  browse: BrowsePage
  buyModal: BuyWithCryptoModal
  navbar: Navbar
  sellModal: SellModal
  signIn: SignInPage
}

const pageObjectFixtures: Fixtures<MarketplacePages, object, PlaywrightTestArgs & PlaywrightTestOptions> = {
  account: async ({ page }, use) => {
    await use(new AccountPage(page))
  },
  asset: async ({ page }, use) => {
    await use(new AssetPage(page))
  },
  authModal: async ({ page }, use) => {
    await use(new AuthorizationModal(page))
  },
  browse: async ({ page }, use) => {
    await use(new BrowsePage(page))
  },
  buyModal: async ({ page }, use) => {
    await use(new BuyWithCryptoModal(page))
  },
  navbar: async ({ page }, use) => {
    await use(new Navbar(page))
  },
  sellModal: async ({ page }, use) => {
    await use(new SellModal(page))
  },
  signIn: async ({ page }, use) => {
    await use(new SignInPage(page))
  }
}

export const marketplaceTest = base.extend<MarketplacePages>(pageObjectFixtures)

/**
 * On-chain wallet fixtures, layered on top of the POM-bundle walletTest.
 *
 *  - `walletPool` is **worker-scoped**: setupWalletPool() runs once per
 *    worker process, the result is cached, and every test in that worker
 *    that destructures the fixture sees the same pool. Under --workers=1
 *    (the only supported mode for the marketplace-onchain project) this
 *    means one pool initialization per CI run — identical to today's
 *    beforeAll(setupWalletPool) semantics, but without the per-describe
 *    ceremony.
 *
 *  - `sellerWallet` / `buyerWallet` are **test-scoped**: each runs once per
 *    test that destructures it, calling setupTestWallet(page, role.privateKey)
 *    against the shared pool and exposing { address } to the test.
 *
 * Off-chain specs that don't destructure these fixtures don't trigger the
 * pool setup, so they continue to run without WALLET_A_PRIVATE_KEY etc. in
 * env (which is the contract documented in web/CLAUDE.md "Marketplace tests
 * — off-chain specs require no .env").
 */
export const walletTest = baseWalletTest
  .extend<MarketplacePages>(pageObjectFixtures)
  .extend<object, { walletPool: WalletPool }>({
    walletPool: [
      async ({}, use) => {
        const pool = await setupWalletPool()
        await use(pool)
      },
      { scope: 'worker' }
    ]
  })
  .extend<{ sellerWallet: { address: string }; buyerWallet: { address: string } }>({
    sellerWallet: async ({ page, walletPool }, use) => {
      const { address } = await setupTestWallet(page, walletPool.seller.privateKey)
      await use({ address })
    },
    buyerWallet: async ({ page, walletPool }, use) => {
      const { address } = await setupTestWallet(page, walletPool.buyer.privateKey)
      await use({ address })
    }
  })
