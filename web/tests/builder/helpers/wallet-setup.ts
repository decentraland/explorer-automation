import type { Page } from '@playwright/test'
import { polygonAmoy, sepolia } from 'viem/chains'
import { privateKeyToAddress } from 'viem/accounts'
import { injectAuthIdentity, installInjectedWalletMock } from '../../../shared/helpers/auth-identity.js'
import { setupBroadcastWallet } from '../../../shared/helpers/broadcast-wallet.js'
import { mockExistingProfile } from '../../../shared/helpers/profile.js'
import { rpcUrl } from '../../../shared/helpers/network.js'
import { pinFeatureFlags } from './feature-flags.js'

/**
 * Pre-navigation wallet setup for the builder dapp, must be called BEFORE the
 * first `page.goto`. Sibling of the marketplace `setupTestWallet` — same
 * load-bearing compose order — plus the builder-specific feature-flag pin.
 *
 * The builder authenticates its server writes with the SSO identity seeded by
 * `injectAuthIdentity` (localStorage `single-sign-on-<addr>`, read by
 * `lib/api/auth.ts` via `localStorageGetIdentity`), NOT with wallet signatures
 * — so off-chain specs need no broadcast layer and no RPC env vars, which is
 * what lets them run on ephemeral wallets with an empty `.env`.
 *
 * `broadcast: true` (on-chain specs only) adds the viem broadcast layer.
 * Wallet stays on Sepolia — builder Polygon writes (createCollection,
 * setMinters/Managers, issueTokens, Committee.manageCollection) are relayed
 * meta-transactions through transactions-server, never user broadcasts.
 * Reads RPC URLs from env, so it requires SEPOLIA_RPC_URL/POLYGON_AMOY_RPC_URL.
 * No contract allowlists: collection contract addresses are dynamic per run.
 */
export interface SetupBuilderWalletOptions {
  broadcast?: boolean
}

export async function setupBuilderWallet(
  page: Page,
  privateKey: `0x${string}`,
  options: SetupBuilderWalletOptions = {}
): Promise<{ address: string }> {
  const address = privateKeyToAddress(privateKey)
  await injectAuthIdentity(page, privateKey)
  await installInjectedWalletMock(page, privateKey)
  await mockExistingProfile(page, address)
  if (options.broadcast) {
    await setupBroadcastWallet(page, {
      privateKey,
      rpcUrls: {
        [polygonAmoy.id]: rpcUrl(polygonAmoy.id),
        [sepolia.id]: rpcUrl(sepolia.id)
      },
      initialChainId: sepolia.id
    })
  }
  await pinFeatureFlags(page)
  return { address }
}
