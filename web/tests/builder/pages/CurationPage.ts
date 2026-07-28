import type { Page, Locator } from '@playwright/test'
import { withEnv } from '../../../shared/helpers/url.js'

/**
 * Committee curation queue (`/builder/curation`, components/CurationPage) —
 * renders NotFound for non-committee wallets.
 *
 * Locator ← builder source mapping:
 *  - search filter   ← curation_page.search_placeholder "Search by name or owner address"
 *  - rows            ← CurationPage/CollectionRow renders `.CollectionRow` (Table.Row with
 *                       an explicit class); the whole row is clickable and navigates to
 *                       the item editor with ?reviewing=true
 *
 * NOTE: never drive "Assign to me" before a FIRST approval — it creates a
 * pending curation the approval saga won't reconcile (see publish-lifecycle).
 */
export class CurationPage {
  constructor(private readonly page: Page) {}

  async goto(): Promise<void> {
    await this.page.goto(withEnv('curation'))
  }

  searchField(): Locator {
    return this.page.getByPlaceholder('Search by name or owner address')
  }

  collectionRow(name: string): Locator {
    return this.page.locator('.CollectionRow').filter({ hasText: name })
  }

  /** Narrows the queue to the collection, then opens its review editor. */
  async openReview(name: string): Promise<void> {
    await this.searchField().fill(name)
    await this.collectionRow(name).click()
    await this.page.waitForURL(/item-editor/, { timeout: 30_000 })
  }
}
