import type { Page } from '@playwright/test'
import { uniqueCollectionName } from './names.js'
import type { CollectionsPage } from '../pages/CollectionsPage.js'
import type { CreateCollectionModal } from '../pages/CreateCollectionModal.js'

/** Collection detail routes end in the collection's uuid. */
export const COLLECTION_DETAIL_URL = /\/collections\/[0-9a-f]{8}-[0-9a-f-]{27}/i

/**
 * Creates a standard collection through the UI (the flow under test in
 * collection-management; setup for everything else) and waits for the
 * redirect to the new collection's detail page.
 */
export async function createCollectionViaUi(
  page: Page,
  collections: CollectionsPage,
  createCollectionModal: CreateCollectionModal
): Promise<{ id: string; name: string; detailUrl: string }> {
  const name = uniqueCollectionName()
  await collections.goto()
  await collections.createCollectionButton().click()
  await createCollectionModal.create(name)
  await page.waitForURL(COLLECTION_DETAIL_URL, { timeout: 30_000 })
  const detailUrl = page.url()
  const id = new URL(detailUrl).pathname.split('/').pop() ?? ''
  return { id, name, detailUrl }
}
