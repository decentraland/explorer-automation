import type { Page, Response } from '@playwright/test'

export interface CaptureListingResponseOptions {
  /** Wait window for the /v1/trades POST. Default 240s — auth-modal relayer
   *  POST (setApprovalForAll meta-tx) can be slow before the trade signature
   *  fires. */
  timeout?: number
}

export interface ListingResult {
  tradeId: string
}

/**
 * Captures the marketplace-api `/v1/trades` POST that the SPEC has just
 * triggered by driving the SellModal + AuthorizationModal. UI-free: just
 * waits for the response, asserts 201, and extracts the tradeId.
 *
 * The seller flow is signature-only — no Amoy receipt to wait on. For
 * first-time sellers the dapp also walks an `AuthorizationModal` step that
 * grants ERC721 approval via meta-tx; the spec drives that, but its relayer
 * POST may delay the /v1/trades POST by minutes. The default 240s timeout
 * covers both paths.
 */
export async function captureListingResponse(
  page: Page,
  options: CaptureListingResponseOptions = {}
): Promise<ListingResult> {
  const tradesResponse: Response = await page.waitForResponse(
    res => /\/v1\/trades(\?|$)/.test(res.url()) && res.request().method() === 'POST',
    { timeout: options.timeout ?? 240_000 }
  )

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
