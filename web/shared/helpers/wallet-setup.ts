import type { Page } from '@playwright/test'
import { polygonAmoy, sepolia } from 'viem/chains'
import { privateKeyToAddress } from 'viem/accounts'
import { injectAuthIdentity } from './auth-identity.js'
import { setupBroadcastWallet } from './broadcast-wallet.js'
import { mockExistingProfile } from './profile.js'
import { requireEnv } from './env.js'

/**
 * Pre-navigation wallet setup, must be called BEFORE the first `page.goto`.
 * Combines the three layers any spec that drives a real wallet needs:
 *
 *   1. `injectAuthIdentity` — SSO identity in localStorage + Web3Mock account
 *      override + chain-id mock. Makes the dapp see the wallet as connected
 *      on first paint.
 *   2. `mockExistingProfile` — bypasses the onboarding/avatar setup flow.
 *   3. `setupBroadcastWallet` — `eth_sendTransaction` + `eth_signTypedData_v4` +
 *      read-method passthrough wired to a viem wallet/public client.
 *
 * Wallet stays on Sepolia by default — Polygon Amoy transactions are relayed
 * by transactions-server, never broadcast directly from the user's wallet.
 * Flipping `initialChainId` to Amoy makes the dapp's authorization saga pick
 * the direct-broadcast path, which fails for lack of POL gas.
 */
export async function setupTestWallet(
  page: Page,
  privateKey: `0x${string}`,
  options: { initialChainId?: number } = {}
): Promise<{ address: string }> {
  const address = privateKeyToAddress(privateKey)
  await injectAuthIdentity(page, privateKey)
  await mockExistingProfile(page, address)
  await setupBroadcastWallet(page, {
    privateKey,
    rpcUrls: {
      [polygonAmoy.id]: requireEnv('POLYGON_AMOY_RPC_URL'),
      [sepolia.id]: requireEnv('SEPOLIA_RPC_URL')
    },
    initialChainId: options.initialChainId ?? sepolia.id
  })
  return { address }
}
