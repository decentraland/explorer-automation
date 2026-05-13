import { walletTest as test } from '../../marketplace/fixtures/wallet-fixture.js'
import {
  encodeFunctionData,
  createWalletClient,
  http,
  parseEther,
  parseEventLogs,
  type TransactionReceipt
} from 'viem'
import { polygonAmoy } from 'viem/chains'
import { privateKeyToAccount, generatePrivateKey } from 'viem/accounts'
import { MANA_AMOY } from '../../marketplace/helpers/wallet-pool.js'
import { injectAuthIdentity, installInjectedWalletMock } from '../../../shared/helpers/auth-identity.js'
import { setupBroadcastWallet } from '../../../shared/helpers/broadcast-wallet.js'
import { mockExistingProfile } from '../../../shared/helpers/profile.js'
import { installAutoWalletMockInitScript } from '../helpers/wallet.js'
import { createAuthRequest, pollAuthOutcome, requireTxHash } from '../helpers/auth-server.js'
import { buildAuthChain } from '../../../shared/helpers/identity.js'
import { waitForAmoyReceipt } from '../../../shared/helpers/ethereum.js'
import { requireEnv, optionalEnv } from '../../../shared/helpers/env.js'

/**
 * MANA donation round-trip via the Explorer → auth-site flow.
 *
 * Explorer's "tip a place owner" feature hands an `eth_sendTransaction`
 * (MANA `transfer` on Polygon Amoy) off to the user's browser wallet via
 * the auth-server RequestPage. This spec simulates Explorer's role by
 * minting the request directly against the auth-api, drives the real
 * /auth/requests/<id> UI to approve, and lets the broadcast layer fire a
 * real Polygon Amoy transaction.
 *
 * Round-trip shape:
 *   • Half 1 (E2E)            — walletA tips walletB via RequestPage.
 *   • Half 2 (direct viem)    — walletB returns the same amount on-chain,
 *                               restoring pool balance. No UI replay.
 *
 * Verification strategy: assert the ERC-20 `Transfer` event emitted by
 * THIS spec's tx receipt — never read `balanceOf`. A receipt's `logs`
 * contain only what its own tx emitted, so the assertion is immune to
 * concurrent activity on the same wallets (other marketplace specs, manual
 * top-ups, etc.). The only remaining concurrency constraint is the EOA
 * nonce: two `eth_sendTransaction` calls from the SAME wallet in flight
 * at once race on the nonce. That's still enforced via the `auth-onchain`
 * project's `workers: 1`. For multi-CI-run parallelism, provision separate
 * `WALLET_A/B_PRIVATE_KEY` pairs per concurrent run.
 *
 * Both pool wallets must hold MANA (covered by the existing wallet-pool
 * MIN_WALLET_MANA precheck) AND POL on Polygon Amoy — POL pays gas because
 * this flow is a direct broadcast, not a meta-transaction.
 *
 * Tagged `@on-chain`; tagged `@web @auth` so it sits alongside other
 * auth-site specs. Project routing in playwright.config.ts excludes
 * `@on-chain` from the default `web` project so this spec runs only via
 * the dedicated `auth-onchain` project (workers=1).
 */

const erc20TransferAbi = [
  {
    name: 'transfer',
    type: 'function',
    stateMutability: 'nonpayable',
    inputs: [
      { name: 'to', type: 'address' },
      { name: 'amount', type: 'uint256' }
    ],
    outputs: [{ type: 'bool' }]
  }
] as const

const erc20TransferEventAbi = [
  {
    name: 'Transfer',
    type: 'event',
    anonymous: false,
    inputs: [
      { name: 'from', type: 'address', indexed: true },
      { name: 'to', type: 'address', indexed: true },
      { name: 'value', type: 'uint256', indexed: false }
    ]
  }
] as const

/**
 * Asserts that the MANA `Transfer(from, to, value)` event with the exact
 * expected args appears in the receipt. Receipt logs are limited to what
 * THIS tx emitted, so this assertion is concurrency-safe — any other tip
 * happening on the same wallets at the same time lives in a different
 * receipt and cannot affect this match.
 */
function assertManaTransferInReceipt(
  receipt: TransactionReceipt,
  expected: { from: string; to: string; value: bigint }
): void {
  const transfers = parseEventLogs({
    abi: erc20TransferEventAbi,
    logs: receipt.logs,
    eventName: 'Transfer'
  })
  const match = transfers.find(
    t =>
      t.address.toLowerCase() === MANA_AMOY.toLowerCase() &&
      t.args.from.toLowerCase() === expected.from.toLowerCase() &&
      t.args.to.toLowerCase() === expected.to.toLowerCase() &&
      t.args.value === expected.value
  )
  if (!match) {
    throw new Error(
      `Expected MANA Transfer ${expected.from} → ${expected.to} value=${expected.value} not found in receipt. ` +
        `Receipt logs: ${JSON.stringify(transfers.map(t => ({ from: t.args.from, to: t.args.to, value: String(t.args.value) })))}`
    )
  }
}

const { expect } = test

const haveOnChainConfig = (): boolean =>
  Boolean(
    optionalEnv('WALLET_A_PRIVATE_KEY') &&
      optionalEnv('WALLET_B_PRIVATE_KEY') &&
      optionalEnv('POLYGON_AMOY_RPC_URL') &&
      optionalEnv('SEPOLIA_RPC_URL')
  )

test.describe('@web @auth @on-chain MANA donation round-trip (RequestPage)', () => {
  test.skip(!haveOnChainConfig(), 'Requires on-chain wallet config')
  test.describe.configure({ timeout: 420_000 })

  test('walletA tips walletB via RequestPage, walletB returns the tip on-chain', async ({ page, walletPool }) => {
    const sender = walletPool.seller
    const receiver = walletPool.buyer
    const tipAmount = parseEther(optionalEnv('MARKETPLACE_TEST_TIP_AMOUNT_MANA') ?? '0.01')

    const amoyRpc = requireEnv('POLYGON_AMOY_RPC_URL')
    const sepoliaRpc = requireEnv('SEPOLIA_RPC_URL')

    // ─── Half 1: UI E2E — walletA tips walletB via RequestPage ───────────
    await injectAuthIdentity(page, sender.privateKey)
    await installInjectedWalletMock(page, sender.privateKey, { chainId: polygonAmoy.id })
    await mockExistingProfile(page, sender.address as `0x${string}`)
    await setupBroadcastWallet(page, {
      privateKey: sender.privateKey,
      rpcUrls: { [polygonAmoy.id]: amoyRpc, 11155111: sepoliaRpc },
      initialChainId: polygonAmoy.id,
      // Defensive: MANA is the only legitimate target for this spec.
      allowedTargets: [MANA_AMOY]
    })

    const data = encodeFunctionData({
      abi: erc20TransferAbi,
      functionName: 'transfer',
      args: [receiver.address, tipAmount]
    })

    // Auth chain — required for any non-`dcl_personal_sign` method.
    const ephemeralKey = generatePrivateKey()
    const ephemeralAddress = privateKeyToAccount(ephemeralKey).address
    const expiration = new Date(Date.now() + 24 * 60 * 60 * 1000)
    const authChain = await buildAuthChain(sender.privateKey, ephemeralAddress, expiration)

    const txParams = { from: sender.address, to: MANA_AMOY, data, value: '0x0' }
    const { requestId } = await createAuthRequest('eth_sendTransaction', [txParams], authChain)
    expect(requestId).toBeTruthy()

    await installAutoWalletMockInitScript(page, sender.address)
    await page.goto(`/auth/requests/${requestId}`, { waitUntil: 'load' })

    const allowBtn = page.locator('[data-testid="wallet-interaction-allow-button"]')
    await allowBtn.waitFor({ state: 'visible', timeout: 30_000 })
    await allowBtn.click()

    // The broadcast layer waits for the receipt before resolving
    // __sendTransaction (see broadcast-wallet.ts), so the dapp won't post
    // the outcome to auth-api until the Amoy tx is mined. Amoy receipts
    // have been observed up to ~3 min — use a 240s polling timeout.
    const outcome = await pollAuthOutcome(requestId, 240_000)
    expect(outcome.sender.toLowerCase()).toBe(sender.address.toLowerCase())
    const txHash = requireTxHash(outcome)
    expect(txHash).toMatch(/^0x[0-9a-f]{64}$/i)

    const receipt = await waitForAmoyReceipt({ txHash, rpcUrl: amoyRpc })
    assertManaTransferInReceipt(receipt, {
      from: sender.address,
      to: receiver.address,
      value: tipAmount
    })

    // ─── Half 2: direct chain reset — walletB → walletA via viem ────────
    const receiverWallet = createWalletClient({
      account: privateKeyToAccount(receiver.privateKey),
      chain: polygonAmoy,
      transport: http(amoyRpc)
    })
    const resetHash = await receiverWallet.writeContract({
      address: MANA_AMOY,
      abi: erc20TransferAbi,
      functionName: 'transfer',
      args: [sender.address, tipAmount],
      chain: polygonAmoy
    })
    const resetReceipt = await waitForAmoyReceipt({ txHash: resetHash, rpcUrl: amoyRpc })
    assertManaTransferInReceipt(resetReceipt, {
      from: receiver.address,
      to: sender.address,
      value: tipAmount
    })
  })
})
