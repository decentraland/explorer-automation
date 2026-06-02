import { mainnet, sepolia, polygon, polygonAmoy } from 'viem/chains'
import { getBaseUrl, requireEnv } from './env.js'

/**
 * The Ethereum + Polygon chain ids a Decentraland dapp expects the connected
 * wallet to be on, for a given environment.
 *
 * A wallet reporting a chain outside this pair makes the dapp render a blocking
 * "Wrong Network" modal whose Semantic-UI dimmer overlays the navbar — which
 * silently breaks any post-connect interaction (e.g. opening the user menu to
 * log out). The pair must therefore match the env the dapp boots with.
 */
export interface AppChains {
  /**
   * Ethereum "auth" chain. The value reported via `eth_chainId` and persisted
   * in `decentraland-connect-storage-key`. The wallet stays on this chain even
   * for Polygon writes — the meta-tx flow signs an EIP-712 payload whose domain
   * carries the Polygon chain id, it does not switch the wallet.
   */
  ethereum: number
  /** Polygon chain — MANA + collectibles secondary network. */
  polygon: number
}

const MAINNET: AppChains = { ethereum: mainnet.id, polygon: polygon.id }
const TESTNET: AppChains = { ethereum: sepolia.id, polygon: polygonAmoy.id }

/**
 * True when the dapp boots against testnets (Sepolia + Polygon Amoy), matching
 * the env `withEnv` puts in the `?env=` query — so the wallet's reported chain
 * can never diverge from the env the dapp actually runs in:
 *
 *  - `MARKETPLACE_ENV=dev`  → testnet (also the default when unset, mirroring
 *    `withEnv`'s `?? 'dev'`).
 *  - `MARKETPLACE_ENV=prod` → mainnet.
 *  - `MARKETPLACE_ENV=''` (explicitly blanked, so `withEnv` appends no `?env=`)
 *    → the dapp falls back to its host default: `.zone` is dev (testnet), every
 *    other host (`.org`, `.today`) is mainnet.
 *
 * Reads env at call time so per-test overrides are honoured.
 */
function isTestnet(): boolean {
  const env = process.env.MARKETPLACE_ENV ?? 'dev'
  if (env === 'dev') return true
  if (env) return false
  return new URL(getBaseUrl()).host.endsWith('decentraland.zone')
}

/**
 * The chains the dapp expects the connected wallet to be on, derived from the
 * SAME inputs `withEnv` uses to build the URL. Keeping the wallet's reported
 * chain in lockstep with the dapp's env is what avoids the "Wrong Network"
 * modal that otherwise overlays (and blocks) the navbar.
 */
export function resolveAppChains(): AppChains {
  return isTestnet() ? TESTNET : MAINNET
}

/**
 * The Ethereum auth chain id for the resolved env — the value to report via
 * `eth_chainId` and seed into `decentraland-connect-storage-key`.
 */
export function appChainId(): number {
  return resolveAppChains().ethereum
}

/**
 * chainId → env var holding that chain's RPC URL. Maps to a var *name*, not the
 * URL itself: RPC URLs carry provider API keys (secrets) and must never be
 * hardcoded in the repo — the value is read from `.env` / Actions secrets at
 * call time. Only testnet chains are mapped: on-chain tests never broadcast on
 * mainnet (real funds), so a mainnet chain id is a configuration error.
 */
const RPC_ENV_VAR: Readonly<Record<number, string>> = {
  [sepolia.id]: 'SEPOLIA_RPC_URL',
  [polygonAmoy.id]: 'POLYGON_AMOY_RPC_URL'
}

/**
 * Resolves the RPC URL for a chain by reading its mapped env var. Throws on an
 * unmapped (e.g. mainnet) chain so a misconfigured on-chain run fails loudly
 * instead of silently broadcasting against the wrong network.
 */
export function rpcUrl(chainId: number): string {
  const varName = RPC_ENV_VAR[chainId]
  if (!varName) {
    throw new Error(
      `No RPC env var mapped for chain ${chainId}. On-chain tests run on testnet only ` +
        `(Sepolia ${sepolia.id}, Polygon Amoy ${polygonAmoy.id}).`
    )
  }
  return requireEnv(varName)
}
