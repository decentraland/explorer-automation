import { walletTest as test } from '../fixtures/wallet-fixture.js'
import { setupTestWallet } from '../../../shared/helpers/wallet-setup.js'
import { optionalEnv } from '../../../shared/helpers/env.js'
import { executePrimaryBuy } from '../helpers/primary-buy.js'
import { executeListing } from '../helpers/listing.js'
import { executeAcceptListing } from '../helpers/accept-listing.js'
import { setupWalletPool, type WalletPool } from '../helpers/wallet-pool.js'
import { type AssetType } from '../pages/AssetPage.js'

const WALLET_A_PRIVATE_KEY = optionalEnv('WALLET_A_PRIVATE_KEY')
const WALLET_B_PRIVATE_KEY = optionalEnv('WALLET_B_PRIVATE_KEY')
const POLYGON_AMOY_RPC_URL = optionalEnv('POLYGON_AMOY_RPC_URL')
const SEPOLIA_RPC_URL = optionalEnv('SEPOLIA_RPC_URL')
const ITEM_CONTRACT = optionalEnv('MARKETPLACE_TEST_ITEM_CONTRACT')
const ITEM_ID = optionalEnv('MARKETPLACE_TEST_ITEM_ID')
const ITEM_TYPE = (optionalEnv('MARKETPLACE_TEST_ITEM_TYPE') ?? 'item') as AssetType
const LISTING_PRICE_MANA = optionalEnv('MARKETPLACE_TEST_LISTING_PRICE_MANA') ?? '1'

const PLACEHOLDER_KEY = '0x0000000000000000000000000000000000000000000000000000000000000000'
const PLACEHOLDER_ADDR = '0x0000000000000000000000000000000000000000'

const haveOnChainConfig =
  Boolean(WALLET_A_PRIVATE_KEY) &&
  WALLET_A_PRIVATE_KEY !== PLACEHOLDER_KEY &&
  Boolean(WALLET_B_PRIVATE_KEY) &&
  WALLET_B_PRIVATE_KEY !== PLACEHOLDER_KEY &&
  WALLET_A_PRIVATE_KEY !== WALLET_B_PRIVATE_KEY &&
  Boolean(POLYGON_AMOY_RPC_URL) &&
  Boolean(SEPOLIA_RPC_URL) &&
  Boolean(ITEM_CONTRACT) &&
  ITEM_CONTRACT !== PLACEHOLDER_ADDR &&
  Boolean(ITEM_ID)

/**
 * End-to-end "buy primary → list → accept secondary" loop using the shared
 * two-wallet pool. Roles are assigned at runtime by `setupWalletPool()` —
 * the wealthier wallet plays seller (it does the primary mint, the largest
 * MANA spend). Over many runs the assignment naturally inverts and balances
 * stay equalized.
 *
 * Each run is self-contained: the seller mints a fresh NFT, lists it, and
 * the buyer accepts. No pre-existing on-chain state required, no NFT
 * survives.
 *
 * Step 1 (seller): primary-buy mints a wearable to the seller wallet.
 *   Wallet stays on Sepolia; the buy is relayed on Amoy.
 * Step 2 (seller): list the just-minted NFT via the OffChainMarketplaceV2
 *   trade signature → POST /v1/trades. Off-chain; no Amoy receipt.
 * Step 3 (buyer): accept the listing — `OffChainMarketplace.accept(_trades)`
 *   wrapped in `executeMetaTransaction` (selector 0xd8ed1acc), POST
 *   /v1/transactions, Amoy mines.
 *
 * Adding a new on-chain flow (e.g. bidding: WALLET_A buys → WALLET_B bids
 * → WALLET_A accepts the bid): create a new spec file in this directory,
 * tag it `@marketplace @on-chain`, wrap the flow in `describe.serial`, call
 * `setupWalletPool()` in `beforeAll`, and compose helpers from
 * `tests/marketplace/helpers/`. The on-chain project's `--workers=1`
 * invocation guarantees no two on-chain specs share the wallet pool
 * concurrently.
 */
test.describe.serial('@marketplace @on-chain marketplace buy-and-sell loop', () => {
  // Two Amoy receipts (primary mint + accept) plus the listing signature
  // stack — the project default of 120s isn't enough.
  test.describe.configure({ timeout: 420_000 })

  test.skip(
    !haveOnChainConfig,
    'On-chain spec requires WALLET_A_PRIVATE_KEY, WALLET_B_PRIVATE_KEY (distinct), RPC URLs, and MARKETPLACE_TEST_ITEM_* in .env'
  )

  let pool: WalletPool

  // Carries the listed NFT identity from the seller test to the buyer test.
  // Each test gets its own page/context, so wallet state doesn't bleed —
  // these closure variables are the only shared signal.
  let listedContract: string | undefined
  let listedTokenId: string | undefined
  let listingTradeId: string | undefined

  test.beforeAll(async () => {
    pool = await setupWalletPool()
  })

  test('seller mints + lists an NFT', async ({ page, navbar, asset, buyModal, authModal, sellModal }) => {
    await setupTestWallet(page, pool.seller.privateKey)

    const minted = await executePrimaryBuy(
      { page, navbar, asset, buyModal, authModal },
      { contract: ITEM_CONTRACT!, itemId: ITEM_ID!, type: ITEM_TYPE }
    )
    listedContract = minted.contract
    listedTokenId = minted.tokenId

    const trade = await executeListing({ page, navbar, sellModal, authModal }, minted, LISTING_PRICE_MANA)
    listingTradeId = trade.tradeId
  })

  test('buyer accepts the listing', async ({ page, navbar, asset, buyModal }) => {
    test.expect(listingTradeId, 'seller test must produce a listing first').toBeTruthy()
    test.expect(listedContract && listedTokenId, 'seller test must capture the listed NFT identity').toBeTruthy()

    await setupTestWallet(page, pool.buyer.privateKey)

    const result = await executeAcceptListing(
      { page, navbar, asset, buyModal },
      { contract: listedContract!, tokenId: listedTokenId! }
    )
    test.expect(result.txHash).toMatch(/^0x[0-9a-f]{64}$/i)
  })
})
