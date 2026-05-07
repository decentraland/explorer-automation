import type { Page, Response } from '@playwright/test'
import { createPublicClient, http, type Hex } from 'viem'
import { polygonAmoy } from 'viem/chains'
import { requireEnv } from '../../../shared/helpers/env.js'
import type { AssetPage } from '../pages/AssetPage.js'
import type { BuyWithCryptoModal } from '../pages/BuyWithCryptoModal.js'
import type { Navbar } from '../pages/Navbar.js'

export interface AcceptListingTarget {
  contract: string
  tokenId: string
}

export interface AcceptListingResult {
  txHash: Hex
}

/**
 * Drives the buyer side of a peer-to-peer secondary sale.
 *
 * Wallet must already be set up by the caller (via `setupTestWallet(...)`).
 * Flow: navigate to the listed NFT → BUY WITH MANA → submit the
 * BuyWithCryptoModal → dapp POSTs `OffChainMarketplace.accept(_trades)`
 * wrapped in `executeMetaTransaction` (selector 0xd8ed1acc) to
 * `/v1/transactions` → relayer broadcasts on Amoy → wait for the receipt →
 * wait for the dapp's `/success` URL.
 *
 * Same `/v1/transactions` endpoint as the primary-buy flow; only the inner
 * calldata differs.
 */
export async function executeAcceptListing(
  ctx: {
    page: Page
    navbar: Navbar
    asset: AssetPage
    buyModal: BuyWithCryptoModal
  },
  listed: AcceptListingTarget
): Promise<AcceptListingResult> {
  const { page, navbar, asset, buyModal } = ctx

  await asset.goto('nft', listed.contract, listed.tokenId)
  await navbar.waitForConnected(60_000)

  await asset.buyWithCryptoButton().waitFor({ state: 'visible', timeout: 30_000 })
  await asset.clickBuyWithCrypto()

  await buyModal.waitForOpen()
  await buyModal.switchNetworkIfPrompted()

  const txResponsePromise: Promise<Response> = page.waitForResponse(
    res => /\/v1\/transactions(\?|$)/.test(res.url()) && res.request().method() === 'POST',
    { timeout: 120_000 }
  )

  await buyModal.submitBuy()

  const txResponse = await txResponsePromise
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
    throw new Error(`Accept-listing tx ${txHash} reverted on Amoy`)
  }

  await page.waitForURL(/\/success(\?|$|\/)/, { timeout: 30_000 })

  return { txHash }
}
