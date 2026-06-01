import { getBaseUrl } from './env.js'

/**
 * Single source of truth for the marketplace-api host. Used by any helper or
 * spec that hits `marketplace-api.decentraland.{org,zone}` directly (e.g.
 * the NFT-indexer poll after a primary mint).
 *
 * Resolution rule (highest priority first):
 *
 *  1. `MARKETPLACE_API_BASE_URL` env var — explicit override, used verbatim
 *     (trailing slash stripped). Set this if you want to point the indexer
 *     reads at a different host (e.g. a staging mirror).
 *
 *  2. Host (`getBaseUrl()` → `WEB_BASE_URL`) is `decentraland.today` — throw.
 *     Marketplace has no `.today` deployment: the `.today` dapp reads from
 *     `marketplace-api.decentraland.org` (mainnet data). Running marketplace
 *     tests against `.today` would mix testnet expectations with mainnet
 *     results, so we refuse loudly rather than silently produce wrong
 *     assertions. Use `WEB_BASE_URL=https://decentraland.org` (optionally with
 *     `MARKETPLACE_ENV=dev` for the testnet bridge) or `.zone` instead.
 *
 *  3. Legacy bridge: host is `decentraland.org` AND `MARKETPLACE_ENV=dev` —
 *     return `marketplace-api.decentraland.zone`. This preserves the "run the
 *     prod dapp host with `?env=dev` against the testnet indexer" workflow,
 *     which is the only way CI can reach the testnet backend (`.zone` itself is
 *     gated behind Cloudflare Access).
 *
 *  4. Otherwise derive from the host: `marketplace-api.<host>`. So
 *     `WEB_BASE_URL=https://decentraland.zone` → `https://marketplace-api.decentraland.zone`,
 *     `WEB_BASE_URL=https://decentraland.org`  → `https://marketplace-api.decentraland.org`
 *     (production indexer with mainnet data).
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

  const host = new URL(getBaseUrl()).host
  const env = process.env.MARKETPLACE_ENV ?? 'dev'

  if (host === 'decentraland.today') {
    throw new Error(
      'Marketplace tests do not support WEB_BASE_URL=https://decentraland.today — ' +
        'the `.today` dapp uses `marketplace-api.decentraland.org` (mainnet data), ' +
        'which would mix testnet expectations with mainnet results. ' +
        'Use WEB_BASE_URL=https://decentraland.org (optionally with MARKETPLACE_ENV=dev) ' +
        'or WEB_BASE_URL=https://decentraland.zone.'
    )
  }

  if (host === 'decentraland.org' && env === 'dev') {
    return 'https://marketplace-api.decentraland.zone'
  }

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
