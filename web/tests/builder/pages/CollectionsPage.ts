import type { Page, Locator } from '@playwright/test'
import { withEnv } from '../../../shared/helpers/url.js'

/**
 * Builder collections overview (`/builder/collections`).
 *
 * Locator ← builder source mapping (builder has ~zero testids in these flows;
 * locators bind to i18n strings from src/modules/translation/languages/en.json
 * and stable page classes — see web/CLAUDE.md "Locator priority"):
 *  - "Create Collection" button        ← collections_page.new_collection (CollectionsPage.tsx renderMainActions)
 *  - "Create Linked Wearables Collection" ← collections_page.new_third_party_collection
 *  - empty state                        ← collections_page.empty_description
 *  - signed-out prompt                  ← global.sign_in_required rendered by components/SignInRequired (.SignInRequired)
 */
export class CollectionsPage {
  constructor(private readonly page: Page) {}

  async goto(): Promise<void> {
    await this.page.goto(withEnv('collections'))
  }

  createCollectionButton(): Locator {
    return this.page.getByRole('button', { name: 'Create Collection', exact: true })
  }

  collectionLink(name: string): Locator {
    return this.page.getByText(name, { exact: true })
  }

  async openCollection(name: string): Promise<void> {
    await this.collectionLink(name).click()
  }

  emptyStateMessage(): Locator {
    return this.page.getByText('You have no collections yet', { exact: false })
  }

  // TODO(testid): propose a testid for the SignInRequired container.
  signInRequiredMessage(): Locator {
    return this.page.locator('.SignInRequired')
  }

  signInLink(): Locator {
    return this.signInRequiredMessage().getByRole('link', { name: 'Sign in' })
  }
}
