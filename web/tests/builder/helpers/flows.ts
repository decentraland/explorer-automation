import path from 'node:path'
import { fileURLToPath } from 'node:url'
import type { Page } from '@playwright/test'
import { expect } from '../../../shared/fixtures/base-test.js'
import { uniqueCollectionName } from './names.js'
import { selectDropdownOption } from './semantic.js'
import type { CollectionsPage } from '../pages/CollectionsPage.js'
import type { CreateCollectionModal } from '../pages/CreateCollectionModal.js'
import type { CreateSingleItemModal } from '../pages/CreateSingleItemModal.js'

export const FIXTURE_FILES = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../fixtures/files')

// GLB import runs Babylon validation, thumbnail rendering, and a builder-api
// size calculation before the details step appears.
export const MODEL_PROCESSING_TIMEOUT = 90_000

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

/**
 * Drives the CreateSingleItemModal details step for a wearable that was just
 * dropped: name, Both body shapes, Common rarity (high supply — the phase-3
 * mint tests depend on it), Lower Body category, Save.
 */
export async function fillWearableDetails(createItemModal: CreateSingleItemModal, itemName: string): Promise<void> {
  await expect(createItemModal.nameField()).toBeVisible({ timeout: MODEL_PROCESSING_TIMEOUT })
  await createItemModal.nameField().fill(itemName)
  await createItemModal.bodyShapeOption('both').click()
  await selectDropdownOption(createItemModal.raritySelect(), 'Common')
  await selectDropdownOption(createItemModal.categorySelect(), 'Lower Body')
  await createItemModal.saveButton().click({ timeout: 60_000 })
}
