import type { Page, Locator } from '@playwright/test'
import { withEnv } from '../../../shared/helpers/url.js'

/**
 * Standard collection detail page (`/builder/collections/:id`,
 * components/CollectionDetailPage).
 *
 * Locator ← builder source mapping:
 *  - page root `.CollectionDetailPage`      ← component root class
 *  - name header `.name-container .name`    ← CollectionDetailPage.tsx name Header; row click opens EditCollectionNameModal
 *  - "Add Items" `.action-button.add-items` ← collection_detail_page.add_item; class disambiguates from
 *                                             the empty-state `.empty-action-button` with the same label
 *  - header context menu                    ← CollectionContextMenu (Dropdown, ellipsis-horizontal icon inside
 *                                             the header `.actions-container`; item rows have their own ellipsis)
 *  - menu entries (role="option")           ← collection_context_menu.copy_urn "Copy URN", global.delete "Delete"
 *  - item rows                              ← CollectionItem renders a Table.Row (CSS-module classes — no stable
 *                                             root class); located via role=row + item-name text
 *  - delete-item menu entry                 ← collection_item.delete_item "Delete item" → opens .DeleteItemModal
 */
export class CollectionDetailPage {
  constructor(private readonly page: Page) {}

  /**
   * The item table is split across tabs — wearables (default) and emotes
   * (`?tab=emote`); an emote row is invisible on the default tab.
   */
  async goto(collectionId: string, options: { tab?: 'wearable' | 'emote' } = {}): Promise<void> {
    const query = options.tab ? `?tab=${options.tab}` : ''
    await this.page.goto(withEnv(`collections/${collectionId}${query}`))
  }

  root(): Locator {
    return this.page.locator('.CollectionDetailPage')
  }

  // TODO(testid): propose a testid for the collection-name header.
  collectionName(): Locator {
    return this.root().locator('.name-container .name')
  }

  /** The whole header row is clickable and opens EditCollectionNameModal (unpublished only). */
  async openEditName(): Promise<void> {
    await this.collectionName().click()
  }

  /**
   * "Add Items" renders in two mutually exclusive places: the header
   * `.action-button.add-items` (only when the collection has items) and the
   * empty-state `.empty-action-button` CTA (only when it doesn't). Exactly one
   * exists at a time, so the accessible-name locator covers both.
   */
  addItemsButton(): Locator {
    return this.root().getByRole('button', { name: 'Add Items', exact: true })
  }

  // TODO(testid): propose a testid for the CollectionContextMenu trigger.
  contextMenuTrigger(): Locator {
    return this.root().locator('.actions-container i.ellipsis.horizontal.icon')
  }

  contextMenuItem(name: string): Locator {
    return this.page.getByRole('option', { name, exact: true })
  }

  async openContextMenu(): Promise<void> {
    await this.contextMenuTrigger().click()
  }

  /**
   * Semantic Confirm rendered by components/ConfirmDelete — header
   * "Delete "<name>"?". Semantic's default confirm label is "OK"; the regex
   * also tolerates a decentraland-ui "Confirm" override.
   */
  deleteConfirmButton(): Locator {
    return this.page.locator('.ui.modal').getByRole('button', { name: /^(ok|confirm)$/i })
  }

  itemRow(itemName: string): Locator {
    return this.page.getByRole('row').filter({ hasText: itemName })
  }

  async openItemMenu(itemName: string): Promise<void> {
    await this.itemRow(itemName).locator('button:has(i.ellipsis.horizontal.icon)').click()
  }

  /** DeleteItemModal (registry-name root class) — Confirm/Cancel. */
  deleteItemModalConfirm(): Locator {
    return this.page.locator('.ui.modal.DeleteItemModal').getByRole('button', { name: 'Confirm' })
  }
}
