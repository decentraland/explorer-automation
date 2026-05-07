import { test as base, type Fixtures, type PlaywrightTestArgs, type PlaywrightTestOptions } from '@playwright/test'
import { walletTest as baseWalletTest } from '../../../shared/fixtures/wallet-fixture.js'
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

export const walletTest = baseWalletTest.extend<MarketplacePages>(pageObjectFixtures)
