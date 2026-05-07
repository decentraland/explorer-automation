import type { Page, Response } from '@playwright/test'
import type { AuthorizationModal } from '../pages/AuthorizationModal.js'
import type { Navbar } from '../pages/Navbar.js'
import type { SellModal } from '../pages/SellModal.js'

export interface ListingTarget {
  contract: string
  tokenId: string
}

export interface ListingResult {
  tradeId: string
}

/**
 * Drives the marketplace's "List for sale" flow end-to-end.
 *
 * Wallet must already be set up by the caller (via `setupTestWallet(...)`).
 * The flow is signature-only — no Amoy receipt to wait on. The seller signs
 * an EIP-712 trade (OffChainMarketplaceV2 v2 domain on Amoy), which the dapp
 * POSTs to marketplace-api `/v1/trades`. First-time sellers also walk an
 * `AuthorizationModal` step that grants ERC721 approval via meta-tx.
 *
 * Returns the `tradeId` from the marketplace-api response. Throws if the
 * response is not 201 or if the body lacks an id.
 */
export async function executeListing(
  ctx: {
    page: Page
    navbar: Navbar
    sellModal: SellModal
    authModal: AuthorizationModal
  },
  nft: ListingTarget,
  priceMana: string
): Promise<ListingResult> {
  const { page, navbar, sellModal, authModal } = ctx

  await sellModal.goto('nft', nft.contract, nft.tokenId)
  await navbar.waitForConnected(60_000)
  await sellModal.waitForLoaded()

  // The auth step's relayer POST (setApprovalForAll meta-tx) can be slow —
  // observed taking minutes in some runs. The /v1/trades POST happens AFTER
  // auth completes, so the wait window has to cover both.
  const tradesResponsePromise: Promise<Response> = page.waitForResponse(
    res => /\/v1\/trades(\?|$)/.test(res.url()) && res.request().method() === 'POST',
    { timeout: 240_000 }
  )

  await sellModal.fillAndSubmit(priceMana)
  await authModal.authorizeAndSign()

  const tradesResponse = await tradesResponsePromise
  if (tradesResponse.status() !== 201) {
    throw new Error(`marketplace-api /v1/trades responded ${tradesResponse.status()}: ${await tradesResponse.text()}`)
  }
  const body = (await tradesResponse.json()) as Record<string, unknown>
  const data = body.data as { id?: string } | undefined
  const tradeId = data?.id ?? (typeof body.id === 'string' ? body.id : undefined)
  if (!tradeId) {
    throw new Error(`marketplace-api response missing trade id: ${JSON.stringify(body)}`)
  }
  return { tradeId }
}
