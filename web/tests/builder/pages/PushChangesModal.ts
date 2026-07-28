import type { Page, Locator } from '@playwright/test'

/**
 * Standard-collection re-review modal (components/Modals/PushCollectionChangesModal
 * — registry class root). Title ← push_collection_changes_modal.title
 * "Push Changes"; confirm ← global.proceed "Proceed". Proceeding POSTs a new
 * pending CollectionCuration — no transaction.
 */
export class PushChangesModal {
  constructor(private readonly page: Page) {}

  root(): Locator {
    return this.page.locator('.ui.modal.PushCollectionChangesModal')
  }

  proceedButton(): Locator {
    return this.root().getByRole('button', { name: 'Proceed' })
  }
}
