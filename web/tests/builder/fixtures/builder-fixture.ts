import type { Fixtures, PlaywrightTestArgs, PlaywrightTestOptions } from '@playwright/test'
import { generatePrivateKey } from 'viem/accounts'
// base-test (not @playwright/test) so even the POM-only flavor carries the
// CF Access route + browser-log diagnostics; the wallet flavors inherit the
// same through walletTest's context override.
import { test as base } from '../../../shared/fixtures/base-test.js'
import { walletTest as baseWalletTest } from '../../../shared/fixtures/wallet-fixture.js'
import { setupBuilderWallet } from '../helpers/wallet-setup.js'
import { cleanupUserCollections, cleanupUserItems, deleteCollectionCascade } from '../helpers/builder-server.js'
import { builderTestWalletKey } from '../helpers/test-wallet.js'
import { CollectionsPage } from '../pages/CollectionsPage.js'
import { CollectionDetailPage } from '../pages/CollectionDetailPage.js'
import { CreateCollectionModal } from '../pages/CreateCollectionModal.js'
import { CreateSingleItemModal } from '../pages/CreateSingleItemModal.js'
import { EditCollectionNameModal } from '../pages/EditCollectionNameModal.js'

/**
 * Builder test fixtures. Three flavors (one per spec file — repo convention):
 *
 *  - `builderTest`             — POMs only, no wallet. For signed-out specs.
 *  - `builderWalletTest`       — POMs + Synpress mock + auto `ephemeralWallet`
 *    (fresh key per test). ONLY for specs whose semantics require isolation:
 *    empty-state, foreign-wallet visibility, non-committee gating. Teardown
 *    best-effort deletes everything the key created.
 *  - `builderSharedWalletTest` — POMs + Synpress mock + auto `testWallet`
 *    (WALLET_A when configured, ephemeral fallback — see test-wallet.ts) and a
 *    `trackedCollections` array fixture: push created collection ids and the
 *    teardown cascade-deletes exactly those, keeping the shared wallet's dev
 *    DB clean without touching its other collections.
 *
 * On-chain specs (publish lifecycle, collection operations) get their own
 * creator/curator fixtures in a later phase.
 */
export type BuilderPages = {
  collections: CollectionsPage
  collectionDetail: CollectionDetailPage
  createCollectionModal: CreateCollectionModal
  createItemModal: CreateSingleItemModal
  editNameModal: EditCollectionNameModal
}

const pageObjectFixtures: Fixtures<BuilderPages, object, PlaywrightTestArgs & PlaywrightTestOptions> = {
  collections: async ({ page }, use) => {
    await use(new CollectionsPage(page))
  },
  collectionDetail: async ({ page }, use) => {
    await use(new CollectionDetailPage(page))
  },
  createCollectionModal: async ({ page }, use) => {
    await use(new CreateCollectionModal(page))
  },
  createItemModal: async ({ page }, use) => {
    await use(new CreateSingleItemModal(page))
  },
  editNameModal: async ({ page }, use) => {
    await use(new EditCollectionNameModal(page))
  }
}

export const builderTest = base.extend<BuilderPages>(pageObjectFixtures)

export const builderWalletTest = baseWalletTest
  .extend<BuilderPages>(pageObjectFixtures)
  .extend<{ ephemeralWallet: { address: string; privateKey: `0x${string}` } }>({
    // `auto` — every builderWalletTest test gets a fresh signed-in wallet
    // without destructuring the fixture. On-chain specs (fixed creator/curator
    // wallets) must NOT build on builderWalletTest for exactly this reason —
    // they get their own fixture chain in a later phase.
    ephemeralWallet: [
      async ({ page }, use) => {
        const privateKey = generatePrivateKey()
        const { address } = await setupBuilderWallet(page, privateKey)
        await use({ address, privateKey })
        await cleanupUserCollections(privateKey)
        await cleanupUserItems(privateKey)
      },
      { auto: true }
    ]
  })

export const builderSharedWalletTest = baseWalletTest
  .extend<BuilderPages>(pageObjectFixtures)
  .extend<{ testWallet: { address: string; privateKey: `0x${string}` } }>({
    testWallet: [
      async ({ page }, use) => {
        const privateKey = builderTestWalletKey()
        const { address } = await setupBuilderWallet(page, privateKey)
        await use({ address, privateKey })
      },
      { auto: true }
    ]
  })
  .extend<{ trackedCollections: string[] }>({
    trackedCollections: async ({ testWallet }, use) => {
      const ids: string[] = []
      await use(ids)
      for (const id of ids) {
        await deleteCollectionCascade(testWallet.privateKey, id).catch((error: unknown) =>
          console.warn(`tracked-collection cleanup skipped for ${id}: ${String(error)}`)
        )
      }
    }
  })
