import type { Page } from '@playwright/test'
import { polygonAmoy, sepolia } from 'viem/chains'
import { privateKeyToAddress } from 'viem/accounts'
import { injectAuthIdentity, installInjectedWalletMock } from '../../../shared/helpers/auth-identity.js'
import { setupBroadcastWallet } from '../../../shared/helpers/broadcast-wallet.js'
import { mockExistingProfile } from '../../../shared/helpers/profile.js'
import { requireEnv } from '../../../shared/helpers/env.js'

/**
 * Pre-navigation wallet setup, must be called BEFORE the first `page.goto`.
 * Marketplace-specific: composes the four layers any spec that drives a real
 * wallet against the marketplace dapp needs.
 *
 *   1. `injectAuthIdentity` — SSO + decentraland-connect localStorage seeding.
 *      Wallet-agnostic.
 *   2. `installInjectedWalletMock` — Web3Mock account override + chain-id
 *      mock. Required for Synpress-backed tests; skip for real-wallet tests.
 *   3. `mockExistingProfile` — bypasses the onboarding/avatar setup flow.
 *   4. `setupBroadcastWallet` — `eth_sendTransaction` + `eth_signTypedData_v4` +
 *      read-method passthrough wired to a viem wallet/public client.
 *
 * Wallet stays on Sepolia by default — Polygon Amoy transactions are relayed
 * by transactions-server, never broadcast directly from the user's wallet.
 * Flipping `initialChainId` to Amoy makes the dapp's authorization saga pick
 * the direct-broadcast path, which fails for lack of POL gas.
 */
export interface SetupTestWalletOptions {
  initialChainId?: number
  /**
   * Optional contract allowlists forwarded to `setupBroadcastWallet`. Tests
   * that broadcast on-chain (currently only the @on-chain buy-and-sell spec)
   * should pass `marketplaceAllowedContracts()` from `./contracts.js`. Tests
   * that don't broadcast (account, connect-wallet) can omit.
   */
  allowedContracts?: {
    targets?: ReadonlyArray<`0x${string}`>
    typedData?: ReadonlyArray<`0x${string}`>
  }
}

export async function setupTestWallet(
  page: Page,
  privateKey: `0x${string}`,
  options: SetupTestWalletOptions = {}
): Promise<{ address: string }> {
  const address = privateKeyToAddress(privateKey)
  await injectAuthIdentity(page, privateKey)
  await installInjectedWalletMock(page, privateKey)
  await mockExistingProfile(page, address)
  await setupBroadcastWallet(page, {
    privateKey,
    rpcUrls: {
      [polygonAmoy.id]: requireEnv('POLYGON_AMOY_RPC_URL'),
      [sepolia.id]: requireEnv('SEPOLIA_RPC_URL')
    },
    initialChainId: options.initialChainId ?? sepolia.id,
    allowedTargets: options.allowedContracts?.targets,
    allowedTypedDataContracts: options.allowedContracts?.typedData
  })
  return { address }
}
