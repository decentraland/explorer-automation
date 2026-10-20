import path from 'node:path'
import { fileURLToPath } from 'node:url'
import type { BrowserContext, Page } from '@playwright/test'
import { test, expect } from '../../../shared/fixtures/base-test.js'
import { newWalletContext } from '../helpers/wallet-context.js'
import { deleteCollectionCascade, sweepStaleQaCollections } from '../helpers/builder-server.js'
import { builderTestWalletKey } from '../helpers/test-wallet.js'
import { createCollectionViaUi } from '../helpers/flows.js'
import { selectDropdownOption } from '../helpers/semantic.js'
import { inflateGlb } from '../helpers/glb.js'
import { CollectionsPage } from '../pages/CollectionsPage.js'
import { CollectionDetailPage } from '../pages/CollectionDetailPage.js'
import { CreateCollectionModal } from '../pages/CreateCollectionModal.js'
import { CreateSingleItemModal } from '../pages/CreateSingleItemModal.js'
import { ItemEditorPage } from '../pages/ItemEditorPage.js'

const FIXTURE_FILES = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../fixtures/files')

// GLB import runs Babylon validation, thumbnail rendering, and a builder-api
// size calculation before the details step appears.
const MODEL_PROCESSING_TIMEOUT = 90_000

/**
 * Item upload flows (wearable / emote / smart wearable) — builder-server
 * writes on an ephemeral wallet, no on-chain activity. The upload itself is
 * the product under test, so files always go through the UI dropzone.
 *
 * Deliberately a SERIAL flow on ONE shared browser context and ONE shared
 * collection: the collection is created once (first stage) and every upload
 * lands in it, mirroring how a creator actually works and avoiding a
 * wallet + collection re-setup per test. A failed stage skips the rest —
 * flow semantics, not independent-test semantics. Trade-off: the shared
 * context is created in beforeAll via newWalletContext, so the per-test
 * screenshot/trace artifacts that attach to fixture-owned pages are not
 * captured for this file.
 */
test.describe('@builder item upload', () => {
  test.describe.configure({ mode: 'serial', timeout: 240_000 })

  const privateKey = builderTestWalletKey()
  let context: BrowserContext
  let page: Page
  let collectionDetail: CollectionDetailPage
  let createItemModal: CreateSingleItemModal

  test.beforeAll(async ({ browser }) => {
    // crash recovery: previous runs killed before afterAll leave QA-prefixed
    // collections on the shared wallet — sweep the stale ones (age-gated so a
    // concurrent run's live collection is never touched)
    await sweepStaleQaCollections(privateKey)
    ;({ context, page } = await newWalletContext(browser, privateKey))
    collectionDetail = new CollectionDetailPage(page)
    createItemModal = new CreateSingleItemModal(page)
  })

  test.afterAll(async () => {
    // delete exactly this run's collection (id set by the first stage);
    // best-effort — a failed first stage leaves nothing to delete
    await deleteCollectionCascade(privateKey, collectionId).catch((error: unknown) =>
      console.warn(`item-upload cleanup skipped: ${String(error)}`)
    )
    await context.close()
  })

  let collectionId: string

  test('creates the collection that hosts the uploaded items', async () => {
    const { id, name } = await createCollectionViaUi(page, new CollectionsPage(page), new CreateCollectionModal(page))
    collectionId = id
    await expect(collectionDetail.collectionName()).toHaveText(name, { timeout: 15_000 })
  })

  test('uploads a wearable GLB and creates an item with category and rarity', async () => {
    await collectionDetail.addItemsButton().click()
    await createItemModal.uploadFile(path.join(FIXTURE_FILES, 'wearable-pants.glb'))
    await expect(createItemModal.nameField()).toBeVisible({ timeout: MODEL_PROCESSING_TIMEOUT })
    await createItemModal.nameField().fill('QA wearable pants')
    await createItemModal.bodyShapeOption('both').click()
    await selectDropdownOption(createItemModal.raritySelect(), 'Common')
    await selectDropdownOption(createItemModal.categorySelect(), 'Lower Body')
    await createItemModal.saveButton().click({ timeout: 60_000 })
    await expect(createItemModal.root()).toBeHidden({ timeout: 90_000 })
    await expect(collectionDetail.itemRow('QA wearable pants')).toBeVisible({ timeout: 20_000 })
  })

  test('uploads an emote GLB and verifies it plays in the item editor', async () => {
    await collectionDetail.addItemsButton().click()
    await createItemModal.uploadFile(path.join(FIXTURE_FILES, 'emote-chef-kiss.glb'))
    await expect(createItemModal.nameField()).toBeVisible({ timeout: MODEL_PROCESSING_TIMEOUT })
    await createItemModal.nameField().fill('QA emote chef kiss')
    await selectDropdownOption(createItemModal.raritySelect(), 'Common')
    await selectDropdownOption(createItemModal.categorySelect(), 'Fun')
    await selectDropdownOption(createItemModal.playModeSelect(), 'Play Once')
    // "next" routes to the thumbnail screenshot step; its Save enables once
    // the WearablePreview iframe finishes rendering the emote, and for emotes
    // that Save IS the final submit — the app then routes into the item editor
    await createItemModal.nextButton().click()
    // The thumbnail step's Save silently no-ops while the WearablePreview
    // controller is still initializing (EditThumbnailStep only wires it on the
    // emote's SECOND load event, and handleSave optional-chains through an
    // undefined controller) — so click-and-check until the submit lands. The
    // catch makes the click best-effort once the modal starts submitting
    // (button disabled) or has closed.
    await expect(async () => {
      await createItemModal
        .saveButton()
        .click({ timeout: 2_000 })
        .catch(() => undefined)
      expect(page.url()).toMatch(/item-editor/)
    }).toPass({ timeout: 120_000, intervals: [3_000] })

    // EmoteControls only mounts once the preview controller is ready, so its
    // visibility is the "preview loaded" signal. Playback proof: the frame
    // slider advances while the emote animates. Entry state (auto-playing vs
    // idle) is nondeterministic and the play/pause ICON is unreliable (its
    // state hangs off events the preview iframe doesn't always echo back), so
    // each attempt blindly clicks the toggle BUTTON and then requires frame
    // movement: play→moves→pass; pause→static→retry resumes it. Converges in
    // at most two attempts from either entry state.
    const itemEditor = new ItemEditorPage(page)
    await expect(itemEditor.emoteControls()).toBeVisible({ timeout: 90_000 })
    await expect(async () => {
      await itemEditor
        .emotePlayToggle()
        .click({ timeout: 2_000 })
        .catch(() => undefined)
      const before = await itemEditor.emoteFrameSlider().inputValue()
      await expect.poll(() => itemEditor.emoteFrameSlider().inputValue(), { timeout: 5_000 }).not.toBe(before)
    }).toPass({ timeout: 90_000 })

    // back to the collection — emotes render under their own tab
    await collectionDetail.goto(collectionId, { tab: 'emote' })
    await expect(collectionDetail.itemRow('QA emote chef kiss')).toBeVisible({ timeout: 20_000 })
  })

  test('uploads a smart wearable ZIP with a showcase video', async () => {
    // the previous stage left the page on the emote tab; smart wearables are
    // wearables, so reset to the default tab for the end-of-stage assertion
    await collectionDetail.goto(collectionId)
    await collectionDetail.addItemsButton().click()
    await createItemModal.uploadFile(path.join(FIXTURE_FILES, 'smart-wearable-glasses.zip'))
    // any .js inside the zip makes the item a smart wearable, which routes to
    // the video step before the details step
    await expect(createItemModal.videoStepTitle()).toBeVisible({ timeout: MODEL_PROCESSING_TIMEOUT })
    await createItemModal.uploadFile(path.join(FIXTURE_FILES, 'showcase-video.mp4'))
    await createItemModal.saveButton().click({ timeout: 60_000 })
    await expect(createItemModal.nameField()).toBeVisible({ timeout: 60_000 })
    await createItemModal.nameField().fill('QA smart glasses')
    await selectDropdownOption(createItemModal.raritySelect(), 'Common')
    await selectDropdownOption(createItemModal.categorySelect(), 'Eyewear')
    await createItemModal.saveButton().click({ timeout: 60_000 })
    await expect(createItemModal.root()).toBeHidden({ timeout: 120_000 })
    await expect(collectionDetail.itemRow('QA smart glasses')).toBeVisible({ timeout: 20_000 })
  })

  test('rejects a model file over the 3MB size limit', async () => {
    await collectionDetail.addItemsButton().click()
    // an inflated EMOTE, not a wearable: wearable GLBs whose candidate
    // categories include Skin get the 8MB skin allowance at import time
    // (ImportStep handleModelFile), so a 3.5MB wearable legitimately passes —
    // the unconditional 3MB cap is the emote one
    const oversized = inflateGlb(path.join(FIXTURE_FILES, 'emote-chef-kiss.glb'), 3_500_000)
    // the rejection message is TRANSIENT — it renders for ~5s and the modal
    // then silently resets to the pristine dropzone — so drop-and-assert
    // retry as a unit: a missed flash just re-drops (rejection is idempotent)
    await expect(async () => {
      await createItemModal.uploadFile({
        name: 'oversized-emote.glb',
        mimeType: 'model/gltf-binary',
        buffer: oversized
      })
      await expect(createItemModal.fileTooBigError()).toBeVisible({ timeout: 10_000 })
    }).toPass({ timeout: MODEL_PROCESSING_TIMEOUT })
    // leave the page clean for the next stage
    await createItemModal.closeButton().click()
    await expect(createItemModal.root()).toBeHidden({ timeout: 15_000 })
  })

  test('deletes an item from the collection detail page', async () => {
    await collectionDetail.openItemMenu('QA wearable pants')
    await collectionDetail.contextMenuItem('Delete item').click()
    await collectionDetail.deleteItemModalConfirm().click()
    await expect(collectionDetail.itemRow('QA wearable pants')).toHaveCount(0, { timeout: 20_000 })
    // the smart wearable shares the (current) wearable tab — it must survive
    await expect(collectionDetail.itemRow('QA smart glasses')).toBeVisible({ timeout: 15_000 })
  })
})
