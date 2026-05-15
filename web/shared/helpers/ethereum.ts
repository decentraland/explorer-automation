import { createPublicClient, http, type Hex, type TransactionReceipt } from 'viem'
import { polygonAmoy } from 'viem/chains'
import { requireEnv } from './env.js'

/**
 * Single entry point for "wait for an Amoy tx receipt and assert success".
 *
 * Both the primary-buy and accept-listing flows broadcast on Polygon Amoy via
 * the transactions-server relayer. The relayer returns `{ txHash }` from
 * `/v1/transactions`, and the test must then poll Amoy for the receipt to
 * confirm the on-chain effect actually landed (the relayer can return a hash
 * for a tx that later reverts — `status === 'reverted'`).
 *
 * Returns the full `TransactionReceipt` so callers can decode logs (e.g.
 * primary-buy reads the ERC721 Transfer log to extract the minted tokenId).
 *
 * Throws if the receipt's status is not `'success'`. Default timeout is 180s
 * — Amoy receipts have been observed to take up to ~3 min in the wild.
 *
 * The viem public client is constructed per-call. `createPublicClient` is
 * cheap; making this a singleton would couple lifetime to module load order
 * which is awkward for a helper that's also used in tests that mock env vars.
 */
export interface WaitForAmoyReceiptOptions {
  txHash: Hex
  rpcUrl?: string
  timeout?: number
}

export async function waitForAmoyReceipt({
  txHash,
  rpcUrl,
  timeout = 180_000
}: WaitForAmoyReceiptOptions): Promise<TransactionReceipt> {
  const transport = http(rpcUrl ?? requireEnv('POLYGON_AMOY_RPC_URL'))
  const client = createPublicClient({ chain: polygonAmoy, transport })

  const receipt = await client.waitForTransactionReceipt({ hash: txHash, timeout })
  if (receipt.status !== 'success') {
    throw new Error(`Amoy tx ${txHash} reverted (status=${receipt.status})`)
  }
  return receipt
}
