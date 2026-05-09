import type { Page } from '@playwright/test'
import { nftsEndpoint } from '../../../shared/helpers/marketplace-api.js'

/**
 * Polls marketplace-api `/v1/nfts?contractAddress=&tokenId=` until the NFT
 * appears, or the timeout elapses.
 *
 * Mints take a few seconds to be picked up by the subgraph indexer; navigating
 * to `/contracts/<c>/tokens/<id>...` before that returns a "Not found…" view
 * and the form / asset-detail never renders. Always wait for the indexer
 * before driving downstream UI.
 */
export async function waitForNftIndexed(
  page: Page,
  contract: string,
  tokenId: string,
  timeoutMs = 90_000
): Promise<void> {
  const url = nftsEndpoint(contract, tokenId)
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
