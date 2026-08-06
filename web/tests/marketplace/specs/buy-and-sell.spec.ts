import { walletTest as test } from '../fixtures/wallet-fixture.js'
import { optionalEnv } from '../../../shared/helpers/env.js'
import { captureTransactionsPosts, extractRelayerTxHash } from '../helpers/transactions-capture.js'
import { decodeMintFromReceipt, assertErc721ReceivedBy } from '../helpers/mint-decoder.js'
import { waitForNftIndexed } from '../helpers/nft-indexer.js'
import { waitForAmoyReceipt } from '../../../shared/helpers/ethereum.js'
import { captureListingResponse } from '../helpers/listing.js'
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
 * two-wallet pool. Roles are assigned at runtime by the `walletPool` fixture
 * — the wealthier wallet plays seller (it does the primary mint, the
 * largest MANA spend). Over many runs the assignment naturally inverts and
 * balances stay equalized.
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
 * Adding a new on-chain flow (e.g. bidding: seller mints → buyer bids →
 * seller accepts the bid): create a new spec file in this directory, tag it
 * `@marketplace @on-chain`, wrap the flow in `describe.serial`, and
 * destructure `sellerWallet` / `buyerWallet` per test. The on-chain
 * project's `--workers=1` invocation guarantees no two on-chain specs share
 * the wallet pool concurrently.
 */
test.describe.serial('@marketplace @on-chain marketplace buy-and-sell loop', () => {
  // Two Amoy receipts (primary mint + accept) plus the listing signature
  // stack — the project default of 120s isn't enough.
  test.describe.configure({ timeout: 420_000 })

  test.skip(
    !haveOnChainConfig,
    'On-chain spec requires WALLET_A_PRIVATE_KEY, WALLET_B_PRIVATE_KEY (distinct), RPC URLs, and MARKETPLACE_TEST_ITEM_* in .env'
  )

  // describe.serial auto-skips downstream tests on upstream failure — that's
  // the load-bearing contract that makes the non-null assertions on
  // `mintedContract!` / `mintedTokenId!` below safe. If a downstream test
  // ran after an upstream failure, those would crash with "undefined" in
  // confusing ways; describe.serial guarantees they don't.
  let mintedContract: string | undefined
  let mintedTokenId: string | undefined
  let listingTradeId: string | undefined

  test('seller buys an item (primary mint)', async ({ sellerWallet, page, navbar, asset, buyModal, authModal }) => {
    console.log('[buy-and-sell] seller wallet:', sellerWallet.address)

    await asset.goto(ITEM_TYPE, ITEM_CONTRACT!, ITEM_ID!)
    await navbar.waitForConnected(60_000)
    await asset.buyWithCryptoButton().waitFor({ state: 'visible', timeout: 30_000 })
    await asset.clickBuyWithCrypto()

    await buyModal.waitForOpen()
    await buyModal.switchNetworkIfPrompted()

    // Attach the network observer BEFORE submitBuy so we don't miss the POST.
    const capture = captureTransactionsPosts(page)
    try {
      await buyModal.submitBuy()

      // Drive the Authorize→Sign modal if it appears. `authorized` is true only
      // when the Authorize step actually ran (fresh, un-approved wallet), which
      // fires an extra approval POST — so the relayer POST count is 2 when
      // authorized, else 1 (the buy only). The previous Promise.race here could
      // never resolve its "modal driven" branch: the fallback branch returned as
      // soon as the approval POST arrived, which is *before* the Sign button
      // enables, so expectedPosts always collapsed to 1 and `last` was the
      // approval receipt — decodeMintFromReceipt then failed to find the mint
      // (masked today only because the pool wallets happen to be pre-approved).
      const { authorized } = await authModal.authorizeAndSign(15_000)
      const expectedPosts = authorized ? 2 : 1
      await capture.waitFor(expectedPosts, 180_000)

      // Buy is always the last POST. Approved wallets: 1 POST = the buy.
      // Fresh wallets: 2 POSTs = approval (first) + buy (last).
      const last = capture.responses[capture.responses.length - 1]!
      const txHash = await extractRelayerTxHash(last)

      const receipt = await waitForAmoyReceipt({ txHash })
      const { tokenId } = decodeMintFromReceipt(receipt, ITEM_CONTRACT! as `0x${string}`)
      await waitForNftIndexed(page, ITEM_CONTRACT!, tokenId)

      mintedContract = ITEM_CONTRACT
      mintedTokenId = tokenId
    } finally {
      capture.dispose()
    }
  })

  test('seller lists the just-minted NFT', async ({ sellerWallet, page, navbar, sellModal, authModal }) => {
    test.expect(mintedContract && mintedTokenId, 'previous test must mint an NFT first').toBeTruthy()
    console.log('[buy-and-sell] seller wallet:', sellerWallet.address)

    await sellModal.goto('nft', mintedContract!, mintedTokenId!)
    await navbar.waitForConnected(60_000)
    await sellModal.waitForLoaded()

    // The /v1/trades response is fired AFTER the auth-modal relayer POST
    // (setApprovalForAll meta-tx) for first-time sellers. Register the
    // listener BEFORE driving the modal so we don't miss the response.
    const tradePromise = captureListingResponse(page, { timeout: 240_000 })

    await sellModal.fillAndSubmit(LISTING_PRICE_MANA)
    await authModal.authorizeAndSign()

    const trade = await tradePromise
    listingTradeId = trade.tradeId
  })

  test('buyer buys the listed NFT', async ({ buyerWallet, page, navbar, asset, buyModal, authModal }) => {
    test.expect(listingTradeId, 'previous tests must produce a listing first').toBeTruthy()
    test.expect(mintedContract && mintedTokenId, 'previous tests must capture the listed NFT identity').toBeTruthy()
    console.log('[buy-and-sell] buyer wallet:', buyerWallet.address)

    await asset.goto('nft', mintedContract!, mintedTokenId!)
    await navbar.waitForConnected(60_000)
    await asset.buyWithCryptoButton().waitFor({ state: 'visible', timeout: 30_000 })
    await asset.clickBuyWithCrypto()

    await buyModal.waitForOpen()
    await buyModal.switchNetworkIfPrompted()

    // Register the /v1/transactions listener BEFORE submitting; the POST fires
    // from the dapp's saga as soon as submitBuy() returns control.
    const capture = captureTransactionsPosts(page)
    try {
      await buyModal.submitBuy()

      // A buyer not yet MANA-approved to the marketplace operator walks the same
      // Authorize→Sign modal (approval POST + accept POST) as the primary buy;
      // an already-approved buyer fires only the accept POST. Drive the modal
      // and count POSTs off `authorized` — the accept is always the last POST.
      // (The previous helper used a plain waitForResponse that locked onto the
      // FIRST POST, i.e. the approval, and never drove the modal — so an
      // approval-needed buyer would deadlock and, if it didn't, verify the wrong
      // tx.)
      const { authorized } = await authModal.authorizeAndSign(15_000)
      const expectedPosts = authorized ? 2 : 1
      await capture.waitFor(expectedPosts, 180_000)

      const last = capture.responses[capture.responses.length - 1]!
      const txHash = await extractRelayerTxHash(last)

      const receipt = await waitForAmoyReceipt({ txHash })
      // Confirm the NFT actually changed hands: the accept tx must emit an
      // ERC-721 Transfer(seller → buyer, tokenId) on the collection contract.
      assertErc721ReceivedBy(
        receipt,
        mintedContract! as `0x${string}`,
        buyerWallet.address as `0x${string}`,
        mintedTokenId!
      )

      await page.waitForURL(/\/success(\?|$|\/)/, { timeout: 30_000 })
    } finally {
      capture.dispose()
    }
  })
})
