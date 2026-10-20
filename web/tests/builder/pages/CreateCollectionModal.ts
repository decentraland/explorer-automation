import type { Page, Locator } from '@playwright/test'

/**
 * "New Collection" modal (components/Modals/CreateCollectionModal).
 *
 * Root class = decentraland-dapps modal registry name (Modal renders
 * `className={name}` — the repo-wide hook for builder modals).
 * Locator ← source mapping:
 *  - name field   ← create_collection_modal.placeholder "My collection" (max 32 chars)
 *  - "Create"     ← global.create
 *  - duplicate error ← create_collection_modal.error_name_already_in_use
 */
export class CreateCollectionModal {
  constructor(private readonly page: Page) {}

  root(): Locator {
    return this.page.locator('.ui.modal.CreateCollectionModal')
  }

  nameField(): Locator {
    return this.root().getByPlaceholder('My collection')
  }

  createButton(): Locator {
    return this.root().getByRole('button', { name: 'Create' })
  }

  nameAlreadyInUseError(): Locator {
    return this.root().getByText('Name already in use')
  }

  /** Fills the name and submits. Navigation to the detail page is asserted by the spec. */
  async create(name: string): Promise<void> {
    await this.nameField().fill(name)
    await this.createButton().click()
  }
}
