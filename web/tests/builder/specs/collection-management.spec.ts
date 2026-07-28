import { expect } from '@playwright/test'
import { builderSharedWalletTest as test } from '../fixtures/builder-fixture.js'
import { createCollectionViaUi } from '../helpers/flows.js'
import { uniqueCollectionName } from '../helpers/names.js'

/**
 * Off-chain collection CRUD against the dev builder-server, on the SHARED test
 * wallet (WALLET_A when configured — team decision: keeps the dev DB
 * sweepable; ephemeral fallback keeps zero-env runs green). Each test pushes
 * the collections it creates into `trackedCollections`; the fixture teardown
 * cascade-deletes exactly those, never touching the wallet's other
 * collections. Tests in this file run sequentially in one worker (Playwright
 * per-file default), so sharing the wallet is race-free.
 *
 * The empty-state test lives in access-control.spec.ts — it needs a wallet
 * with no collections, which only an ephemeral key guarantees.
 */
test.describe('@builder collection management', () => {
  test('creates a standard collection and shows it in the collections list', async ({
    page,
    collections,
    createCollectionModal,
    trackedCollections
  }) => {
    const { id, name } = await createCollectionViaUi(page, collections, createCollectionModal)
    trackedCollections.push(id)
    await collections.goto()
    await expect(collections.collectionLink(name)).toBeVisible({ timeout: 20_000 })
  })

  test('renames a collection from the detail page', async ({
    page,
    collections,
    createCollectionModal,
    collectionDetail,
    editNameModal,
    trackedCollections
  }) => {
    const { id } = await createCollectionViaUi(page, collections, createCollectionModal)
    trackedCollections.push(id)
    const newName = uniqueCollectionName()
    await collectionDetail.openEditName()
    await editNameModal.rename(newName)
    await expect(collectionDetail.collectionName()).toHaveText(newName, { timeout: 20_000 })
  })

  test('copies the collection URN from the context menu', async ({
    page,
    collections,
    createCollectionModal,
    collectionDetail,
    trackedCollections
  }) => {
    const { id } = await createCollectionViaUi(page, collections, createCollectionModal)
    trackedCollections.push(id)
    await page.context().grantPermissions(['clipboard-read', 'clipboard-write'])
    await collectionDetail.openContextMenu()
    await collectionDetail.contextMenuItem('Copy URN').click()
    const copied = await page.evaluate(() => navigator.clipboard.readText())
    expect(copied).toMatch(/^urn:decentraland:/)
  })

  test('deletes a collection and removes it from the list', async ({
    page,
    collections,
    createCollectionModal,
    collectionDetail
  }) => {
    // not tracked — this test's subject IS the deletion; teardown would 404
    const { name } = await createCollectionViaUi(page, collections, createCollectionModal)
    await collectionDetail.openContextMenu()
    const deleteRequest = page.waitForResponse(
      response => response.request().method() === 'DELETE' && response.url().includes('/collections/'),
      { timeout: 20_000 }
    )
    await collectionDetail.contextMenuItem('Delete').click()
    await collectionDetail.deleteConfirmButton().click()
    await deleteRequest
    // the shared wallet may own other collections — assert THIS one is gone
    await collections.goto()
    await expect(collections.createCollectionButton()).toBeVisible({ timeout: 20_000 })
    await expect(collections.collectionLink(name)).toHaveCount(0)
  })
})
