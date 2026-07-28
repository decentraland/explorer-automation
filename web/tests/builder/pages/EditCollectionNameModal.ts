import type { Page, Locator } from '@playwright/test'

/**
 * "Edit Collection Name" modal (components/Modals/EditCollectionNameModal).
 * Root class = modal registry name. Field label ← global.name "Name";
 * submit ← global.save "Save" (a Form submit button).
 */
export class EditCollectionNameModal {
  constructor(private readonly page: Page) {}

  root(): Locator {
    return this.page.locator('.ui.modal.EditCollectionNameModal')
  }

  nameField(): Locator {
    return this.root().locator('input')
  }

  saveButton(): Locator {
    return this.root().getByRole('button', { name: 'Save' })
  }

  async rename(newName: string): Promise<void> {
    await this.nameField().fill(newName)
    await this.saveButton().click()
  }
}
