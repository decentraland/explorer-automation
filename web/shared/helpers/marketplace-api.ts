/**
 * Single source of truth for the marketplace-api host. Used by any helper or
 * spec that hits `marketplace-api.decentraland.{zone,org}` directly (e.g. the
 * NFT-indexer poll after a primary mint).
 *
 * Resolution rule (highest priority first):
 *
 *  1. `MARKETPLACE_API_BASE_URL` env var — explicit override, used verbatim
 *     (trailing slash stripped). Set this if you want to point the indexer
 *     reads at a different host (e.g. a staging mirror).
 *
 *  2. `MARKETPLACE_ENV === 'dev'` (the default per `withEnv()`) — return the
 *     `.zone` indexer, which carries testnet (Polygon Amoy / Sepolia) data.
 *     The dapp at `.org/marketplace/?env=dev` queries this same host for its
 *     own indexer reads, so the test reads the same data the dapp reads.
 *
 *  3. Otherwise derive from `BASE_URL`'s host: `marketplace-api.<host>`. So
 *     `BASE_URL=https://decentraland.org` → `https://marketplace-api.decentraland.org`
 *     — the production indexer with mainnet data.
 *
 * Reads env vars at call time (not import time) so tests that override env
 * vars in `beforeAll` see the new value.
 *
 * `marketplace-api.decentraland.zone` is publicly reachable — no Cloudflare
 * Access service token needed; only browser navigation to the dapp host
 * (`decentraland.zone` itself) requires those headers.
 */
export function marketplaceApiBaseUrl(): string {
  const explicit = process.env.MARKETPLACE_API_BASE_URL
  if (explicit) return explicit.replace(/\/$/, '')

  const env = process.env.MARKETPLACE_ENV ?? 'dev'
  if (env === 'dev') return 'https://marketplace-api.decentraland.zone'

  const baseUrl = process.env.BASE_URL ?? 'https://decentraland.org'
  const host = new URL(baseUrl).host
  return `https://marketplace-api.${host}`
}

/**
 * Builds a `/v1/nfts?contractAddress=...&tokenId=...` query URL against the
 * resolved marketplace-api host. Used by the post-mint indexer poll to confirm
 * the new NFT is searchable before navigating to its asset page.
 */
export function nftsEndpoint(contractAddress: string, tokenId: string): string {
  return `${marketplaceApiBaseUrl()}/v1/nfts?contractAddress=${contractAddress}&tokenId=${tokenId}`
}
