import type { Page, Locator } from '@playwright/test'
import { withEnv } from '../../../shared/helpers/url.js'

export type AssetType = 'item' | 'nft'

export class AssetPage {
  constructor(private readonly page: Page) {}

  async goto(type: AssetType, contractAddress: string, id: string): Promise<void> {
    const segment = type === 'item' ? 'items' : 'tokens'
    await this.page.goto(withEnv(`contracts/${contractAddress}/${segment}/${id}`))
  }

  /**
   * Asset-page primary "Buy with MANA" CTA — opens the BuyWithCryptoModal.
   * The component renders a `decentraland-ui` Button with a localized label
   * that resolves to "BUY WITH MANA" (no data-testid).
   *
   * Important: the navbar also has a "BUY MANA" top-up button — match
   * "BUY WITH MANA" exactly (with "WITH") to avoid that one.
   */
  buyWithCryptoButton(): Locator {
    return this.page.getByRole('button', { name: /buy with mana/i })
  }

  async clickBuyWithCrypto(): Promise<void> {
    await this.buyWithCryptoButton().click()
  }

  // TODO(testid): replace `[class*="Price"]` with `getByTestId('asset-price')` once
  // the marketplace exposes one. Tracked in AGENTS.md → "Pending marketplace testids".
  priceText(): Locator {
    return this.page.locator('[class*="Price"]').first()
  }
}
