import type { Page, Locator } from '@playwright/test'
import { withEnv } from '../../../shared/helpers/url.js'

export class AccountPage {
  constructor(private readonly page: Page) {}

  /** Visit the connected user's account view. */
  async gotoCurrent(): Promise<void> {
    await this.page.goto(withEnv('account'))
  }

  /** Heading or banner that surfaces the connected address (also on the navbar). */
  addressBanner(): Locator {
    return this.page.locator('[class*="AccountPage"]').getByText(/^0x/).first()
  }

  /** "No items" empty state — fresh wallets land here. */
  emptyState(): Locator {
    return this.page.getByText(/no items|nothing to see here|no results/i)
  }

  // TODO(testid): replace `.AssetCard` with `getByTestId('asset-card')` once
  // the marketplace exposes one. Tracked in AGENTS.md → "Pending marketplace testids".
  ownedItems(): Locator {
    return this.page.locator('.AssetCard')
  }
}
