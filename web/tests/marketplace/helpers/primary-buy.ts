import type { Page, Request, Response } from '@playwright/test'
import type { Hex } from 'viem'
import { waitForAmoyReceipt } from '../../../shared/helpers/ethereum.js'
import type { AssetType } from '../pages/AssetPage.js'

export type PrimaryBuyResult = {
  contract: string
  tokenId: string
  txHash: Hex
}

export type PrimaryBuyConfig = {
  contract: string
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
 * Captures the relayer txHash from a primary-buy flow that the SPEC has just
 * driven through the dapp's UI (asset.goto → clickBuyWithCrypto → buyModal
 * .submitBuy). This helper is deliberately UI-free: it observes the network
 * for the `/v1/transactions` POST(s), decides whether the auth modal is in
 * play, drives the auth modal if so, picks the last POST (the buy, not the
 * approval), waits for the Amoy receipt, and decodes the mint Transfer log
 * to extract the minted tokenId.
 *
 * Wallet setup is the caller's responsibility — a test fixture
 * (`sellerWallet` / `buyerWallet`) covers it via `setupTestWallet`.
 *
 * Why network-observation in the helper instead of the spec: the dual-POST
 * race (first-time wallets fire approval + buy; already-approved wallets
 * fire only buy) needs careful collection logic, and the auth-modal driving
 * is conditional on a network signal. Hand-rolling that in every spec would
 * be error-prone. The helper hides exactly the part Playwright doesn't have
 * a clean primitive for.
 *
 * Returns `{ contract, tokenId, txHash }`. Throws if the relayer rejects,
 * the receipt reverts, or no mint Transfer event appears in the logs.
 */
export async function executePrimaryBuy(
  page: Page,
  authModalSignButton: import('@playwright/test').Locator,
  config: PrimaryBuyConfig,
  authorizeAndSign: (intervalMs?: number) => Promise<boolean>
): Promise<PrimaryBuyResult> {
  // First-time buyers from a fresh wallet need MANA approval before the mint,
  // which means TWO POSTs to /v1/transactions: approval first, then the buy.
  // `page.waitForResponse` would lock onto the FIRST one (the approval) and
  // we'd then try to extract a mint Transfer log from an approval receipt,
  // failing with "No mint Transfer event found". Collect all POSTs and pick
  // the last one once the buy resolves.
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
    // First-time wallets hit the MANA-approval modal here: Authorize signs
    // the approval and POSTs to /v1/transactions, then Confirm transaction
    // signs the buy and POSTs to /v1/transactions. Already-approved wallets
    // skip the modal and the buy POST fires directly.
    //
    // Race the two outcomes (modal opens vs first POST fires) instead of
    // committing to a path on a fixed timeout. Modal-driven flows always
    // render their sign button quickly; if 10s elapses with no button
    // visible we're on the already-approved path. The relayer POST fallback
    // timer keeps its longer 60s window because /v1/transactions can be
    // slow even when no modal is in play.
    const modalDriven = await Promise.race([
      authModalSignButton
        .waitFor({ state: 'visible', timeout: 10_000 })
        .then(async () => authorizeAndSign(2_000))
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
          : ' No POST request was attempted — the spec likely did not trigger the dapp saga.'
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

    const receipt = await waitForAmoyReceipt({ txHash })

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
