import type { Page, Locator } from '@playwright/test'
import { withEnv } from '../../../shared/helpers/url.js'

export class BrowsePage {
  constructor(private readonly page: Page) {}

  async goto(): Promise<void> {
    await this.page.goto(withEnv('browse'))
  }

  // TODO(testid): replace `.AssetCard` with `getByTestId('asset-card')` once
  // the marketplace exposes one. Tracked in AGENTS.md → "Pending marketplace testids".
  assetCards(): Locator {
    return this.page.locator('.AssetCard')
  }

  async waitForResults(timeoutMs = 30_000): Promise<void> {
    await this.assetCards().first().waitFor({ state: 'visible', timeout: timeoutMs })
  }

  /** Click the Nth visible asset card (default first). */
  async openAsset(n = 0): Promise<void> {
    await this.assetCards().nth(n).click()
  }
}
