import type { Page, Request, Response } from '@playwright/test'
import { createPublicClient, http, type Hex } from 'viem'
import { polygonAmoy } from 'viem/chains'
import { requireEnv } from '../../../shared/helpers/env.js'
import type { AssetPage, AssetType } from '../pages/AssetPage.js'
import type { AuthorizationModal } from '../pages/AuthorizationModal.js'
import type { BuyWithCryptoModal } from '../pages/BuyWithCryptoModal.js'
import type { Navbar } from '../pages/Navbar.js'

export type PrimaryBuyResult = {
  contract: string
  tokenId: string
  txHash: Hex
}

export type PrimaryBuyConfig = {
  contract: string
  itemId: string
  type: AssetType
}

// ERC721 Transfer(address indexed from, address indexed to, uint256 indexed tokenId)
const TRANSFER_EVENT_TOPIC = '0xddf252ad1be2c89b69c2b068fc378daa952ba7f163c4a11628f55a4df523b3ef'
const ZERO_TOPIC = '0x0000000000000000000000000000000000000000000000000000000000000000'

// marketplace-api indexer host — dev/zone. Production would be `.org`; lifted
// to a constant so the polling helper below has a single source of truth.
const MARKETPLACE_API_HOST = 'https://marketplace-api.decentraland.zone'

/**
 * Polls marketplace-api `/v1/nfts?contractAddress=&tokenId=` until the NFT
 * appears, or the timeout elapses. Mints take a few seconds to be picked up
 * by the subgraph indexer; navigating to the /sell page before that returns
 * a "Not found…" view and the form never renders.
 */
async function waitForNftIndexed(page: Page, contract: string, tokenId: string, timeoutMs = 90_000): Promise<void> {
  const url = `${MARKETPLACE_API_HOST}/v1/nfts?contractAddress=${contract}&tokenId=${tokenId}`
  const deadline = Date.now() + timeoutMs
  while (Date.now() < deadline) {
    const res = await page.request.get(url)
    if (res.ok()) {
      const body = (await res.json()) as { data?: unknown[] }
      if (Array.isArray(body.data) && body.data.length > 0) return
    }
    await new Promise<void>(resolve => setTimeout(resolve, 2000))
  }
  throw new Error(`NFT ${contract}/${tokenId} not indexed by marketplace-api after ${timeoutMs}ms`)
}

/**
 * Drives the marketplace's primary-buy flow end-to-end and returns the
 * minted NFT's identifier. Wallet setup is the caller's responsibility —
 * call `setupTestWallet(...)` first.
 *
 * Steps: navigate to the asset page → click "Buy with MANA" → submit the
 * BuyWithCryptoModal → relayer broadcasts on Amoy → wait for the receipt →
 * decode the Transfer event log to extract the minted tokenId →
 * wait for the dapp's `/success` URL.
 *
 * Returns `{ contract, tokenId, txHash }`. Throws if the relayer rejects,
 * the receipt reverts, or no mint Transfer event appears in the logs.
 */
export async function executePrimaryBuy(
  ctx: {
    page: Page
    navbar: Navbar
    asset: AssetPage
    buyModal: BuyWithCryptoModal
    authModal: AuthorizationModal
  },
  config: PrimaryBuyConfig
): Promise<PrimaryBuyResult> {
  const { page, navbar, asset, buyModal, authModal } = ctx

  // First-time buyers from a fresh wallet need MANA approval before the mint,
  // which means TWO POSTs to /v1/transactions: approval first, then the buy.
  // `page.waitForResponse` would lock onto the FIRST one (the approval) and
  // we'd then try to extract a mint Transfer log from an approval receipt,
  // failing with "No mint Transfer event found". Collect all POSTs and pick
  // the last one once `/success` confirms the flow finished.
  const txResponses: Response[] = []
  let txRequestsAttempted = 0
  const onResponse = (res: Response) => {
    if (/\/v1\/transactions(\?|$)/.test(res.url()) && res.request().method() === 'POST') {
      txResponses.push(res)
    }
  }
  const onRequest = (req: Request) => {
    if (/\/v1\/transactions(\?|$)/.test(req.url()) && req.method() === 'POST') {
      txRequestsAttempted++
    }
  }
  page.on('response', onResponse)
  page.on('request', onRequest)

  try {
    await asset.goto(config.type, config.contract, config.itemId)
    await navbar.waitForConnected(60_000)
    await asset.buyWithCryptoButton().waitFor({ state: 'visible', timeout: 30_000 })
    await asset.clickBuyWithCrypto()

    await buyModal.waitForOpen()
    await buyModal.switchNetworkIfPrompted()

    await buyModal.submitBuy()
    // First-time wallets hit the MANA-approval modal here: Authorize signs
    // the approval and POSTs to /v1/transactions, then Confirm transaction
    // signs the buy and POSTs to /v1/transactions. Already-approved wallets
    // skip the modal and the buy POST fires directly.
    //
    // Race the two outcomes (modal opens vs first POST fires) instead of
    // committing to a path on a fixed timeout — the modal can take longer
    // than 5s to render under slow loads / cold caches, in which case we
    // used to wrongly conclude "already approved" and wait 180s for a POST
    // the dapp was never going to send. Whichever signal arrives first
    // tells us which path the dapp took.
    const modalDriven = await Promise.race([
      authModal
        .signButton()
        .waitFor({ state: 'visible', timeout: 60_000 })
        .then(async () => {
          // Modal is open; drive it. authorizeAndSign re-checks visibility
          // (resolves instantly here) and returns true once both clicks land.
          return authModal.authorizeAndSign(2_000)
        })
        .catch(() => false),
      (async () => {
        const deadline = Date.now() + 60_000
        while (Date.now() < deadline) {
          if (txResponses.length > 0) return false
          await new Promise<void>(r => setTimeout(r, 250))
        }
        return false
      })()
    ])

    // Approved wallets: 1 POST (the buy). Fresh wallets: 2 POSTs (approval +
    // buy). The buy is always the LAST POST. Poll until we have at least
    // that many.
    const expectedPosts = modalDriven ? 2 : 1
    const deadline = Date.now() + 180_000
    while (Date.now() < deadline && txResponses.length < expectedPosts) {
      await new Promise<void>(resolve => setTimeout(resolve, 500))
    }
    if (txResponses.length < expectedPosts) {
      const hint =
        txRequestsAttempted > txResponses.length
          ? ` ${txRequestsAttempted - txResponses.length} request(s) went out but never received a response — transactions-api.decentraland.zone may be hung or rate-limiting this wallet.`
          : ' No POST request was attempted — submitBuy likely did not trigger the dapp saga.'
      throw new Error(
        `Expected ${expectedPosts} /v1/transactions POST(s), saw ${txResponses.length} response(s) (${txRequestsAttempted} request(s) attempted).${hint}`
      )
    }
    const txResponse = txResponses[txResponses.length - 1]
    if (!txResponse) {
      throw new Error('No /v1/transactions POST observed during primary buy')
    }
    if (!txResponse.ok()) {
      throw new Error(`transactions-server responded ${txResponse.status()}: ${await txResponse.text()}`)
    }
    const body = (await txResponse.json()) as Record<string, unknown>
    const nested = (body.data as { txHash?: string } | undefined)?.txHash
    const txHash = (typeof body.txHash === 'string' ? body.txHash : nested) as Hex | undefined
    if (!txHash || !/^0x[0-9a-f]{64}$/i.test(txHash)) {
      throw new Error(`transactions-server response missing txHash: ${JSON.stringify(body)}`)
    }

    const amoy = createPublicClient({
      chain: polygonAmoy,
      transport: http(requireEnv('POLYGON_AMOY_RPC_URL'))
    })
    const receipt = await amoy.waitForTransactionReceipt({ hash: txHash, timeout: 180_000 })
    if (receipt.status !== 'success') {
      throw new Error(`Primary-buy tx ${txHash} reverted on Amoy`)
    }

    // CollectionStore.buy mints via ERC721 Transfer(address(0), buyer, tokenId).
    // topics[0] = event sig, topics[1] = from (zero for mints), topics[3] = tokenId.
    const mintLog = receipt.logs.find(
      l =>
        l.address.toLowerCase() === config.contract.toLowerCase() &&
        l.topics[0] === TRANSFER_EVENT_TOPIC &&
        l.topics[1] === ZERO_TOPIC
    )
    if (!mintLog || !mintLog.topics[3]) {
      throw new Error(`No mint Transfer event found in receipt for contract ${config.contract} (tx ${txHash})`)
    }
    const tokenId = BigInt(mintLog.topics[3]).toString()

    // Wait for marketplace-api to index the new NFT before returning. Without
    // this, downstream nav to `/contracts/<c>/tokens/<id>...` renders the
    // "Not found…" page because the subgraph hasn't caught up yet.
    await waitForNftIndexed(page, config.contract, tokenId)

    return { contract: config.contract, tokenId, txHash }
  } finally {
    page.off('response', onResponse)
    page.off('request', onRequest)
  }
}
