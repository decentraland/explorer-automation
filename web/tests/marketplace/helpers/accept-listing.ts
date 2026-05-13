import type { Page, Response } from '@playwright/test'
import type { Hex } from 'viem'
import { waitForAmoyReceipt } from '../../../shared/helpers/ethereum.js'

export interface CaptureAcceptListingOptions {
  /** Wait window for the /v1/transactions POST. Default 120s. */
  postTimeout?: number
  /** Wait window for the Amoy receipt. Default 180s (matches waitForAmoyReceipt default). */
  receiptTimeout?: number
}

export interface AcceptListingResult {
  txHash: Hex
}

/**
 * Captures the relayer txHash from a peer-to-peer accept-listing flow that
 * the SPEC has just driven through the dapp's UI (asset.goto →
 * clickBuyWithCrypto → buyModal.submitBuy). UI-free: just waits for the
 * /v1/transactions POST, extracts the txHash, and waits for the Amoy
 * receipt via the shared `waitForAmoyReceipt` helper.
 *
 * Same /v1/transactions endpoint as the primary-buy flow; only the inner
 * calldata differs (`OffChainMarketplace.accept(_trades)` wrapped in
 * `executeMetaTransaction`, selector 0xd8ed1acc).
 *
 * The spec is responsible for navigating to the listed NFT, opening the
 * BuyWithCryptoModal, and submitting it. It is also responsible for any
 * post-tx UI assertions (e.g. `/success` URL).
 */
export async function captureAcceptListingTxHash(
  page: Page,
  options: CaptureAcceptListingOptions = {}
): Promise<AcceptListingResult> {
  const txResponse: Response = await page.waitForResponse(
    res => /\/v1\/transactions(\?|$)/.test(res.url()) && res.request().method() === 'POST',
    { timeout: options.postTimeout ?? 120_000 }
  )

  if (!txResponse.ok()) {
    throw new Error(`transactions-server responded ${txResponse.status()}: ${await txResponse.text()}`)
  }
  const body = (await txResponse.json()) as Record<string, unknown>
  const nested = (body.data as { txHash?: string } | undefined)?.txHash
  const txHash = (typeof body.txHash === 'string' ? body.txHash : nested) as Hex | undefined
  if (!txHash || !/^0x[0-9a-f]{64}$/i.test(txHash)) {
    throw new Error(`transactions-server response missing txHash: ${JSON.stringify(body)}`)
  }

  await waitForAmoyReceipt({ txHash, timeout: options.receiptTimeout })

  return { txHash }
}
