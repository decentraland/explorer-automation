import { createPublicClient, http, type Address, type Hex } from 'viem'
import { polygonAmoy } from 'viem/chains'
import { privateKeyToAccount } from 'viem/accounts'
import { requireEnv } from '../../../shared/helpers/env.js'

/**
 * Two-EOA wallet pool, shared across on-chain marketplace specs.
 *
 * The chain is a single shared resource that Playwright's per-test isolation
 * doesn't extend to. Two parallel workers using the same EOA on the same item
 * race on the contract nonce — the second tx reverts. The fix is to cap the
 * pool at 2 wallets (the irreducible floor for peer-to-peer flows where buyer
 * and seller must be distinct EOAs) and serialize on-chain specs via
 * `--workers=1` in the npm script for the `marketplace-onchain` project.
 *
 * `setupWalletPool` reads each wallet's MANA balance on Polygon Amoy and
 * assigns roles: the wealthier wallet plays `seller` (primary mint = largest
 * MANA spend), the other plays `buyer`. Over many runs this keeps both
 * balances roughly equal — neither wallet depletes faster than the other.
 *
 * Throws if either wallet falls below `MIN_WALLET_MANA`. Operators top up
 * manually; there's no auto-funding fixture.
 */

// MANA on Polygon Amoy. Source: decentraland-transactions/src/contracts/manaToken.ts
// (ChainId.MATIC_AMOY entry). If this address ever moves, prefer surfacing
// it via env over patching here.
export const MANA_AMOY: Address = '0x7ad72b9f944ea9793cf4055d88f81138cc2c63a0'

// Minimum MANA each wallet must hold before the on-chain suite is allowed
// to start. Covers one primary mint plus a small buffer; wallets near this
// threshold will hit "wallet pool low" and need a top-up.
export const MIN_WALLET_MANA = 100n * 10n ** 18n

const erc20BalanceOfAbi = [
  {
    name: 'balanceOf',
    type: 'function',
    stateMutability: 'view',
    inputs: [{ name: 'account', type: 'address' }],
    outputs: [{ type: 'uint256' }]
  }
] as const

export interface WalletRole {
  privateKey: Hex
  address: Address
}

export interface WalletPool {
  seller: WalletRole
  buyer: WalletRole
}

export async function setupWalletPool(): Promise<WalletPool> {
  const keyA = requireEnv('WALLET_A_PRIVATE_KEY') as Hex
  const keyB = requireEnv('WALLET_B_PRIVATE_KEY') as Hex
  if (keyA === keyB) {
    throw new Error('WALLET_A_PRIVATE_KEY and WALLET_B_PRIVATE_KEY must be distinct EOAs.')
  }

  const a: WalletRole = { privateKey: keyA, address: privateKeyToAccount(keyA).address }
  const b: WalletRole = { privateKey: keyB, address: privateKeyToAccount(keyB).address }

  const amoy = createPublicClient({
    chain: polygonAmoy,
    transport: http(requireEnv('POLYGON_AMOY_RPC_URL'))
  })
  const [balanceA, balanceB] = await Promise.all([
    amoy.readContract({ address: MANA_AMOY, abi: erc20BalanceOfAbi, functionName: 'balanceOf', args: [a.address] }),
    amoy.readContract({ address: MANA_AMOY, abi: erc20BalanceOfAbi, functionName: 'balanceOf', args: [b.address] })
  ])

  if (balanceA < MIN_WALLET_MANA || balanceB < MIN_WALLET_MANA) {
    throw new Error(
      `Wallet pool low. Each wallet must hold ≥ ${MIN_WALLET_MANA / 10n ** 18n} MANA on Amoy. ` +
        `WALLET_A=${balanceA / 10n ** 18n} MANA (${a.address}), ` +
        `WALLET_B=${balanceB / 10n ** 18n} MANA (${b.address}). ` +
        `Top up the depleted wallet(s) before re-running.`
    )
  }

  // Wealthier wallet plays seller — the primary mint is the largest single
  // MANA spend in the loop, so giving it to the richer wallet keeps both
  // wallets roughly equal over many runs.
  const [seller, buyer] = balanceA >= balanceB ? [a, b] : [b, a]

  // Surface the assignment in test logs — auditing role rotation is the
  // easiest way to confirm balance equalization is actually happening.
  console.log(
    `[wallet-pool] seller=${seller.address} (${(balanceA >= balanceB ? balanceA : balanceB) / 10n ** 18n} MANA) ` +
      `buyer=${buyer.address} (${(balanceA >= balanceB ? balanceB : balanceA) / 10n ** 18n} MANA)`
  )

  return { seller, buyer }
}
