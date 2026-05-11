import { optionalEnv, requireEnv } from '../../../shared/helpers/env.js'

/**
 * Canonical contract addresses the marketplace test wallet is permitted to
 * broadcast to. Wired through `setupTestWallet` → `setupBroadcastWallet`'s
 * `allowedTargets` / `allowedTypedDataContracts` to enforce a server-side
 * (Node-handler) allowlist on the `__sendTransaction` / `__signTypedData`
 * `page.exposeFunction` bridges. Limits the blast radius of an XSS in the
 * marketplace dapp that could otherwise call `window.__sendTransaction` with
 * an arbitrary `to`.
 *
 * Keep this list narrow — every address here is one the test grants the dapp
 * permission to spend testnet MANA / sign typed data against. Adding a new
 * contract is a deliberate decision, not a workaround.
 *
 * Sourced from:
 *  - MANA Amoy: decentraland-transactions/src/contracts/manaToken.ts
 *    (mirrored at `tests/marketplace/helpers/wallet-pool.ts:28`).
 *  - OffChainMarketplaceV2 Amoy: discovered empirically by logging
 *    `domain.verifyingContract` from the dapp's `eth_signTypedData_v4` call
 *    on dev/Amoy across all three @on-chain flows (primary mint, listing,
 *    accept-listing). All three sign against the same address; primaryType
 *    is `MetaTransaction` for primary-buy + accept and `Trade` for listing.
 *    Verify against the dapp source on any major dev redeploy.
 *  - Item contract: per-test, via `MARKETPLACE_TEST_ITEM_CONTRACT` env var.
 *
 * Observed in this codebase: across all three @on-chain flows, the dapp on
 * Polygon Amoy never calls `eth_sendTransaction` — every write is a
 * meta-transaction signed via EIP-712 and POSTed to transactions-server. The
 * `targets` allowlist therefore stays defensive (covers a hypothetical fall-
 * off-the-meta-tx-path approval), but `typedData` is the load-bearing one.
 */

// MANA token on Polygon Amoy. Lowercase.
export const MANA_AMOY: `0x${string}` = '0x7ad72b9f944ea9793cf4055d88f81138cc2c63a0'

// OffChainMarketplaceV2 on Polygon Amoy. Lowercase. The signing target for
// /v1/trades (off-chain listings) and the executor for primary mint +
// accept-listing meta-transactions.
export const OFFCHAIN_MARKETPLACE_V2_AMOY: `0x${string}` = '0x1b67d0e31eeb6b52d8eeed71d3616c2f5b33b8e7'

/**
 * Returns the allowlist for the on-chain marketplace tests. Reads the
 * configured item contract (`MARKETPLACE_TEST_ITEM_CONTRACT`) at call time;
 * callers should invoke this AFTER the on-chain config guard passes.
 */
export function marketplaceAllowedContracts(): {
  targets: ReadonlyArray<`0x${string}`>
  typedData: ReadonlyArray<`0x${string}`>
} {
  const itemContract = requireEnv('MARKETPLACE_TEST_ITEM_CONTRACT').toLowerCase() as `0x${string}`
  // `eth_sendTransaction` targets: rare in meta-tx flows, but present for
  // first-time MANA approvals if the dapp ever falls off the meta-tx path.
  const targets: `0x${string}`[] = [MANA_AMOY, OFFCHAIN_MARKETPLACE_V2_AMOY, itemContract]

  // EIP-712 domain.verifyingContract values the dapp asks us to sign:
  //   - OffChainMarketplaceV2 (listing trade signatures, accept-listing)
  //   - MANA (ERC-2612 permit, if dapp ever uses gas-less approvals)
  const typedData: `0x${string}`[] = [OFFCHAIN_MARKETPLACE_V2_AMOY, MANA_AMOY]

  return { targets, typedData }
}

/**
 * Looser variant: returns just the static contracts (no env reads). Useful for
 * fixtures that need a default before per-test config is loaded.
 */
export function marketplaceStaticAllowedContracts(): {
  targets: ReadonlyArray<`0x${string}`>
  typedData: ReadonlyArray<`0x${string}`>
} {
  // optionalEnv keeps the file importable in off-chain contexts where
  // MARKETPLACE_TEST_ITEM_CONTRACT is unset.
  const itemContract = (optionalEnv('MARKETPLACE_TEST_ITEM_CONTRACT')?.toLowerCase() ?? null) as `0x${string}` | null
  const targets: `0x${string}`[] = [MANA_AMOY, OFFCHAIN_MARKETPLACE_V2_AMOY]
  if (itemContract) targets.push(itemContract)
  const typedData: `0x${string}`[] = [OFFCHAIN_MARKETPLACE_V2_AMOY, MANA_AMOY]
  return { targets, typedData }
}
