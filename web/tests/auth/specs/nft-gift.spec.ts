import { walletTest as test } from '../../marketplace/fixtures/wallet-fixture.js'
import {
  encodeFunctionData,
  createPublicClient,
  createWalletClient,
  http,
  parseEventLogs,
  type TransactionReceipt
} from 'viem'
import { polygonAmoy } from 'viem/chains'
import { privateKeyToAccount, generatePrivateKey } from 'viem/accounts'
import { injectAuthIdentity, installInjectedWalletMock } from '../../../shared/helpers/auth-identity.js'
import { setupBroadcastWallet } from '../../../shared/helpers/broadcast-wallet.js'
import { mockExistingProfile } from '../../../shared/helpers/profile.js'
import { installAutoWalletMockInitScript } from '../helpers/wallet.js'
import { createAuthRequest, pollAuthOutcome, requireTxHash } from '../helpers/auth-server.js'
import { buildAuthChain } from '../../../shared/helpers/identity.js'
import { waitForAmoyReceipt } from '../../../shared/helpers/ethereum.js'
import { requireEnv, optionalEnv } from '../../../shared/helpers/env.js'
import type { WalletPool, WalletRole } from '../../marketplace/helpers/wallet-pool.js'

/**
 * NFT gift round-trip via the Explorer → auth-site flow.
 *
 * Same shape as `mana-donation.spec.ts`. Explorer can hand an NFT transfer
 * off to the user's browser wallet through the same RequestPage mechanism
 * as a place tip; only the calldata differs (`safeTransferFrom` instead of
 * `transfer`).
 *
 * Round-trip:
 *   • Half 1 (E2E)         — current owner gifts the NFT to the other pool
 *                            wallet via RequestPage.
 *   • Half 2 (direct viem) — receiver returns the NFT on-chain, restoring
 *                            ownership.
 *
 * Verification strategy: assert the ERC-721 `Transfer` event emitted by
 * THIS spec's tx receipt — never query `ownerOf` against the live chain
 * for assertions. A receipt's `logs` contain only what its own tx emitted,
 * so the assertion is immune to concurrent activity. (The initial
 * `ownerOf` read is setup — needed to figure out which wallet plays sender
 * — not an assertion; a race there fails loudly on broadcast.)
 *
 * Fixture: reuses `MARKETPLACE_TEST_ITEM_CONTRACT` for the NFT contract
 * and `MARKETPLACE_TEST_GIFT_TOKEN_ID` for the token id. One of the two
 * pool wallets must own the token at start.
 *
 * Both wallets need POL on Amoy for gas (this is a direct broadcast, not
 * a meta-transaction).
 *
 * Residual concurrency constraint: two `eth_sendTransaction` calls from
 * the SAME EOA in flight at once race on the nonce. Project `auth-onchain`
 * enforces `workers: 1` to prevent that. Also: parallel runs of this spec
 * against the SAME token id can't coexist — once half 1 transfers it, the
 * second instance's broadcast finds the token at the wrong owner and
 * reverts. Provision separate token ids per concurrent CI run.
 */

const erc721OwnerOfAbi = [
  {
    name: 'ownerOf',
    type: 'function',
    stateMutability: 'view',
    inputs: [{ name: 'tokenId', type: 'uint256' }],
    outputs: [{ type: 'address' }]
  }
] as const

const erc721SafeTransferFromAbi = [
  {
    name: 'safeTransferFrom',
    type: 'function',
    stateMutability: 'nonpayable',
    inputs: [
      { name: 'from', type: 'address' },
      { name: 'to', type: 'address' },
      { name: 'tokenId', type: 'uint256' }
    ],
    outputs: []
  }
] as const

const erc721TransferEventAbi = [
  {
    name: 'Transfer',
    type: 'event',
    anonymous: false,
    inputs: [
      { name: 'from', type: 'address', indexed: true },
      { name: 'to', type: 'address', indexed: true },
      { name: 'tokenId', type: 'uint256', indexed: true }
    ]
  }
] as const

const { expect } = test

/**
 * Asserts that the ERC-721 `Transfer(from, to, tokenId)` event with the
 * exact expected args appears in the receipt's logs. Receipt logs are
 * scoped to a single tx, so the assertion is concurrency-safe — any other
 * NFT activity happening at the same time lives in a different receipt and
 * cannot affect this match.
 */
function assertNftTransferInReceipt(
  receipt: TransactionReceipt,
  expected: { contract: string; from: string; to: string; tokenId: bigint }
): void {
  const transfers = parseEventLogs({
    abi: erc721TransferEventAbi,
    logs: receipt.logs,
    eventName: 'Transfer'
  })
  const match = transfers.find(
    t =>
      t.address.toLowerCase() === expected.contract.toLowerCase() &&
      t.args.from.toLowerCase() === expected.from.toLowerCase() &&
      t.args.to.toLowerCase() === expected.to.toLowerCase() &&
      t.args.tokenId === expected.tokenId
  )
  if (!match) {
    throw new Error(
      `Expected ERC-721 Transfer ${expected.from} → ${expected.to} tokenId=${expected.tokenId} not found in receipt. ` +
        `Receipt logs: ${JSON.stringify(transfers.map(t => ({ from: t.args.from, to: t.args.to, tokenId: String(t.args.tokenId) })))}`
    )
  }
}

/**
 * Returns the pool wallet roles split into `sender` (current owner of the
 * NFT) and `receiver`. Extracted to a helper so the conditional doesn't sit
 * inside the test body (Playwright lint flags conditionals in tests).
 */
function resolveSenderReceiver(
  pool: WalletPool,
  currentOwner: string,
  context: { contract: string; tokenId: bigint }
): { sender: WalletRole; receiver: WalletRole } {
  const owner = currentOwner.toLowerCase()
  if (owner === pool.seller.address.toLowerCase()) return { sender: pool.seller, receiver: pool.buyer }
  if (owner === pool.buyer.address.toLowerCase()) return { sender: pool.buyer, receiver: pool.seller }
  throw new Error(
    `Neither pool wallet owns token ${context.tokenId} on ${context.contract}. Current owner: ${owner}. ` +
      `Re-fund one of the pool wallets with this NFT before re-running.`
  )
}

const haveOnChainConfig = (): boolean =>
  Boolean(
    optionalEnv('WALLET_A_PRIVATE_KEY') &&
      optionalEnv('WALLET_B_PRIVATE_KEY') &&
      optionalEnv('POLYGON_AMOY_RPC_URL') &&
      optionalEnv('SEPOLIA_RPC_URL') &&
      optionalEnv('MARKETPLACE_TEST_ITEM_CONTRACT') &&
      optionalEnv('MARKETPLACE_TEST_GIFT_TOKEN_ID')
  )

test.describe('@web @auth @on-chain NFT gift round-trip (RequestPage)', () => {
  test.skip(!haveOnChainConfig(), 'Requires on-chain wallet + NFT config')
  test.describe.configure({ timeout: 420_000 })

  test('owner gifts NFT via RequestPage, receiver returns it on-chain', async ({ page, walletPool }) => {
    const nftContract = requireEnv('MARKETPLACE_TEST_ITEM_CONTRACT').toLowerCase() as `0x${string}`
    const tokenId = BigInt(requireEnv('MARKETPLACE_TEST_GIFT_TOKEN_ID'))
    const amoyRpc = requireEnv('POLYGON_AMOY_RPC_URL')
    const sepoliaRpc = requireEnv('SEPOLIA_RPC_URL')
    const pub = createPublicClient({ chain: polygonAmoy, transport: http(amoyRpc) })

    // Resolve sender/receiver by current ownership. Either pool wallet may
    // own the NFT depending on prior runs of this spec.
    const ownerRaw = await pub.readContract({
      address: nftContract,
      abi: erc721OwnerOfAbi,
      functionName: 'ownerOf',
      args: [tokenId]
    })
    const { sender, receiver } = resolveSenderReceiver(walletPool, ownerRaw, {
      contract: nftContract,
      tokenId
    })

    // ─── Half 1: UI E2E — sender gifts NFT via RequestPage ───────────────
    await injectAuthIdentity(page, sender.privateKey)
    await installInjectedWalletMock(page, sender.privateKey, { chainId: polygonAmoy.id })
    await mockExistingProfile(page, sender.address as `0x${string}`)
    await setupBroadcastWallet(page, {
      privateKey: sender.privateKey,
      rpcUrls: { [polygonAmoy.id]: amoyRpc, 11155111: sepoliaRpc },
      initialChainId: polygonAmoy.id,
      // Defensive: only the configured NFT contract is a legitimate target.
      allowedTargets: [nftContract]
    })

    const data = encodeFunctionData({
      abi: erc721SafeTransferFromAbi,
      functionName: 'safeTransferFrom',
      args: [sender.address, receiver.address, tokenId]
    })

    const ephemeralKey = generatePrivateKey()
    const ephemeralAddress = privateKeyToAccount(ephemeralKey).address
    const expiration = new Date(Date.now() + 24 * 60 * 60 * 1000)
    const authChain = await buildAuthChain(sender.privateKey, ephemeralAddress, expiration)

    const txParams = { from: sender.address, to: nftContract, data, value: '0x0' }
    const { requestId } = await createAuthRequest('eth_sendTransaction', [txParams], authChain)
    expect(requestId).toBeTruthy()

    await installAutoWalletMockInitScript(page, sender.address)
    await page.goto(`/auth/requests/${requestId}`, { waitUntil: 'load' })

    const allowBtn = page.locator('[data-testid="wallet-interaction-allow-button"]')
    await allowBtn.waitFor({ state: 'visible', timeout: 30_000 })
    await allowBtn.click()

    const outcome = await pollAuthOutcome(requestId, 240_000)
    expect(outcome.sender.toLowerCase()).toBe(sender.address.toLowerCase())
    const txHash = requireTxHash(outcome)
    expect(txHash).toMatch(/^0x[0-9a-f]{64}$/i)

    const receipt = await waitForAmoyReceipt({ txHash, rpcUrl: amoyRpc })
    assertNftTransferInReceipt(receipt, {
      contract: nftContract,
      from: sender.address,
      to: receiver.address,
      tokenId
    })

    // ─── Half 2: direct chain reset — receiver returns the NFT ──────────
    const receiverWallet = createWalletClient({
      account: privateKeyToAccount(receiver.privateKey),
      chain: polygonAmoy,
      transport: http(amoyRpc)
    })
    const resetHash = await receiverWallet.writeContract({
      address: nftContract,
      abi: erc721SafeTransferFromAbi,
      functionName: 'safeTransferFrom',
      args: [receiver.address, sender.address, tokenId],
      chain: polygonAmoy
    })
    const resetReceipt = await waitForAmoyReceipt({ txHash: resetHash, rpcUrl: amoyRpc })
    assertNftTransferInReceipt(resetReceipt, {
      contract: nftContract,
      from: receiver.address,
      to: sender.address,
      tokenId
    })
  })
})
