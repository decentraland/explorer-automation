import path from 'node:path'
import type { BrowserContext, Page } from '@playwright/test'
import { test, expect } from '../../../shared/fixtures/base-test.js'
import { optionalEnv } from '../../../shared/helpers/env.js'
import { newWalletContext } from '../helpers/wallet-context.js'
import { builderTestWalletKey } from '../helpers/test-wallet.js'
import {
  getCollection,
  getCollectionItems,
  getCommittee,
  getCuration,
  sweepStaleQaCollections
} from '../helpers/builder-server.js'
import { createCollectionViaUi, fillWearableDetails, FIXTURE_FILES } from '../helpers/flows.js'
import { CollectionsPage } from '../pages/CollectionsPage.js'
import { CollectionDetailPage } from '../pages/CollectionDetailPage.js'
import { CreateCollectionModal } from '../pages/CreateCollectionModal.js'
import { CreateSingleItemModal } from '../pages/CreateSingleItemModal.js'
import { ItemEditorPage } from '../pages/ItemEditorPage.js'
import { PublishWizardModal } from '../pages/PublishWizardModal.js'
import { AuthorizationModal } from '../pages/AuthorizationModal.js'
import { ApprovalFlowModal } from '../pages/ApprovalFlowModal.js'
import { RejectionModal } from '../pages/RejectionModal.js'
import { CurationPage } from '../pages/CurationPage.js'
import { PushChangesModal } from '../pages/PushChangesModal.js'

// On-chain lifecycle needs the funded shared wallet plus RPC endpoints for
// the broadcast layer (rpcUrl() reads them at call time).
const haveOnChainConfig = Boolean(
  optionalEnv('WALLET_A_PRIVATE_KEY') && optionalEnv('POLYGON_AMOY_RPC_URL') && optionalEnv('SEPOLIA_RPC_URL')
)

// Each on-chain stage is dominated by relayed meta-tx confirmations on Amoy
// (1-3 min each) and, after publish, subgraph indexing before the server
// flips is_published.
const TX_STAGE_TIMEOUT = 480_000
const SERVER_POLL_TIMEOUT = 300_000
const SERVER_POLL_INTERVALS = [5_000, 10_000]

/**
 * Full standard-collection lifecycle on dev, exercised end to end:
 * publish (MANA fee) → committee approval → item update → re-review →
 * curation rejection → resubmission.
 *
 * ONE wallet drives every role: on dev, WALLET_A is both the creator and a
 * committee member (curator), so a single serial browser context walks the
 * whole flow — no context switching, no role hand-off.
 *
 * Publishing deploys a permanent collection contract on Amoy (~1 MANA fee) —
 * dev-only by design, and the reason this spec lives in the manual-dispatch
 * `builder-onchain` project. The final collection is left published (it
 * cannot be deleted) and its id is logged for BUILDER_TEST_PUBLISHED_COLLECTION_ID
 * (phase-3 operations spec bootstrap).
 *
 * NEVER wait for networkidle here — the forum-post saga retries forever
 * after publish (see web/CLAUDE.md "Builder dapp tests").
 */
test.describe('@builder @on-chain publish and curation lifecycle', () => {
  test.skip(!haveOnChainConfig, 'Set WALLET_A_PRIVATE_KEY, POLYGON_AMOY_RPC_URL and SEPOLIA_RPC_URL')
  test.describe.configure({ mode: 'serial', timeout: 600_000 })

  const privateKey = builderTestWalletKey()
  let context: BrowserContext
  let page: Page
  let address: string
  let collectionDetail: CollectionDetailPage
  let itemEditor: ItemEditorPage
  let curation: CurationPage
  let collectionId: string
  let collectionName: string

  test.beforeAll(async ({ browser }) => {
    await sweepStaleQaCollections(privateKey)
    ;({ context, page, address } = await newWalletContext(browser, privateKey, { broadcast: true }))
    collectionDetail = new CollectionDetailPage(page)
    itemEditor = new ItemEditorPage(page)
    curation = new CurationPage(page)
  })

  test.afterAll(async () => {
    // published collections cannot be deleted — this run's collection stays on
    // dev; log it so it can seed BUILDER_TEST_PUBLISHED_COLLECTION_ID
    console.log(`lifecycle collection: name=${collectionName} id=${collectionId}`)
    await context.close()
  })

  test('wallet is a committee member on dev', async () => {
    const committee = await getCommittee(privateKey)
    expect(committee).toContain(address.toLowerCase())
  })

  test('creates a collection with a wearable ready to publish', async () => {
    const created = await createCollectionViaUi(page, new CollectionsPage(page), new CreateCollectionModal(page))
    collectionId = created.id
    collectionName = created.name

    const createItemModal = new CreateSingleItemModal(page)
    await collectionDetail.addItemsButton().click()
    await createItemModal.uploadFile(path.join(FIXTURE_FILES, 'wearable-pants.glb'))
    await fillWearableDetails(createItemModal, 'QA lifecycle pants')
    await expect(createItemModal.root()).toBeHidden({ timeout: 90_000 })
    await expect(collectionDetail.itemRow('QA lifecycle pants')).toBeVisible({ timeout: 20_000 })
  })

  test('publishes the collection on-chain paying the MANA fee', async () => {
    const wizard = new PublishWizardModal(page)
    const authModal = new AuthorizationModal(page)

    await collectionDetail.publishButton().click()
    await wizard.nameConfirmationField().fill(collectionName)
    await wizard.confirmNameButton().click()
    await wizard.confirmItemsButton().click({ timeout: 30_000 })
    await wizard.acceptContentPolicy('qa-builder@decentraland.org')
    await wizard.continueButton().click()
    await wizard.payWithManaButton().click({ timeout: 30_000 })

    // shows only when the MANA allowance to CollectionManager isn't granted yet
    await authModal.completeIfShown()

    // allowance (maybe) + createCollection meta-txs confirm on Amoy before the
    // wizard reaches the congratulations step
    await expect(wizard.successMessage()).toBeVisible({ timeout: TX_STAGE_TIMEOUT })
  })

  test('collection becomes published once the subgraph indexes it', async () => {
    await expect
      .poll(async () => (await getCollection(privateKey, collectionId)).is_published, {
        timeout: SERVER_POLL_TIMEOUT,
        intervals: SERVER_POLL_INTERVALS
      })
      .toBe(true)
  })

  test('curator approves the collection through the approval flow', async () => {
    const approvalFlow = new ApprovalFlowModal(page)

    // Deliberately NO "Assign to me" before a FIRST approval: self-assigning
    // creates a pending CollectionCuration, and the approval saga only
    // reconciles a pending curation to approved in its already-approved branch
    // (builder src modules/collection/sagas.ts step 7) — after a first-time
    // setApproved TX the record stays pending forever, which suppresses
    // "Publish Updates" and wedges the rest of the lifecycle.
    await curation.goto()
    await curation.openReview(collectionName)

    await itemEditor.approveButton().click({ timeout: 60_000 })
    // rescue meta-tx → catalyst entity deploy → setApproved meta-tx; each
    // button enables when the previous stage lands
    await approvalFlow.rescueConfirmButton().click({ timeout: 60_000 })
    await approvalFlow.uploadButton().click({ timeout: TX_STAGE_TIMEOUT })
    await approvalFlow.enableButton().click({ timeout: TX_STAGE_TIMEOUT })
    await expect(approvalFlow.successTitle()).toBeVisible({ timeout: TX_STAGE_TIMEOUT })
    await approvalFlow.closeButton().click()

    await expect
      .poll(async () => (await getCollection(privateKey, collectionId)).is_approved, {
        timeout: SERVER_POLL_TIMEOUT,
        intervals: SERVER_POLL_INTERVALS
      })
      .toBe(true)
  })

  test('creator updates the item and sends the changes for re-review', async () => {
    const items = await getCollectionItems(privateKey, collectionId)
    const item = items.find(candidate => candidate.name === 'QA lifecycle pants')
    expect(item).toBeDefined()

    await itemEditor.goto(item!.id, collectionId)
    await itemEditor.descriptionField().fill(`updated for re-review ${Date.now()}`, { timeout: 60_000 })
    await itemEditor.saveButton().click({ timeout: 30_000 })
    await expect(itemEditor.saveButton()).toBeDisabled({ timeout: 60_000 })

    // "Publish Updates" renders only once the item computes as UNSYNCED, which
    // needs the approval-deployed catalyst entity to be fetchable — the peer
    // can lag the approval by minutes. Reload until the button materializes.
    await expect(async () => {
      await collectionDetail.goto(collectionId)
      await expect(collectionDetail.publishUpdatesButton()).toBeVisible({ timeout: 15_000 })
    }).toPass({ timeout: SERVER_POLL_TIMEOUT })
    await collectionDetail.publishUpdatesButton().click()
    await new PushChangesModal(page).proceedButton().click({ timeout: 30_000 })

    await expect
      .poll(async () => (await getCuration(privateKey, collectionId))?.status, {
        timeout: SERVER_POLL_TIMEOUT,
        intervals: SERVER_POLL_INTERVALS
      })
      .toBe('pending')
  })

  test('curator rejects the pending curation', async () => {
    const rejectionModal = new RejectionModal(page)

    await curation.goto()
    await curation.openReview(collectionName)
    await itemEditor.rejectButton().click({ timeout: 60_000 })
    // REJECT_CURATION is a server-side PATCH — no transaction
    await rejectionModal.rejectButton().click({ timeout: 30_000 })
    await expect(rejectionModal.verdictMessage()).toBeVisible({ timeout: 60_000 })

    await expect
      .poll(async () => (await getCuration(privateKey, collectionId))?.status, {
        timeout: SERVER_POLL_TIMEOUT,
        intervals: SERVER_POLL_INTERVALS
      })
      .toBe('rejected')
  })

  test('creator resubmits the collection for review after the rejection', async () => {
    await expect(async () => {
      await collectionDetail.goto(collectionId)
      await expect(collectionDetail.publishUpdatesButton()).toBeVisible({ timeout: 15_000 })
    }).toPass({ timeout: SERVER_POLL_TIMEOUT })
    await collectionDetail.publishUpdatesButton().click()
    await new PushChangesModal(page).proceedButton().click({ timeout: 30_000 })

    await expect
      .poll(async () => (await getCuration(privateKey, collectionId))?.status, {
        timeout: SERVER_POLL_TIMEOUT,
        intervals: SERVER_POLL_INTERVALS
      })
      .toBe('pending')
  })
})
