import { walletTest as test } from '../../marketplace/fixtures/wallet-fixture.js'
import { encodeFunctionData, parseEther, parseEventLogs, type TransactionReceipt } from 'viem'
import { polygonAmoy } from 'viem/chains'
import { privateKeyToAccount, generatePrivateKey } from 'viem/accounts'
import { MANA_AMOY } from '../../marketplace/helpers/wallet-pool.js'
import { injectAuthIdentity, installInjectedWalletMock } from '../../../shared/helpers/auth-identity.js'
import { setupBroadcastWallet } from '../../../shared/helpers/broadcast-wallet.js'
import { mockExistingProfile } from '../../../shared/helpers/profile.js'
import {
  authPairedServiceUrl,
  createAuthRequest,
  dappEnvQuery,
  pollAuthOutcome,
  requireTxHash
} from '../helpers/auth-server.js'
import { sendManaMetaTransfer } from '../helpers/mana-meta-tx.js'
import { buildAuthChain } from '../../../shared/helpers/identity.js'
import { waitForAmoyReceipt } from '../../../shared/helpers/ethereum.js'
import { requireEnv, optionalEnv } from '../../../shared/helpers/env.js'
import { withEnv } from '../../../shared/helpers/url.js'

/**
 * MANA tip round-trip via the auth-site RequestPage flow.
 *
 * The auth-site dapp recognises `eth_sendTransaction` requests with MANA
 * `transfer(to, amount)` calldata, renders the "MANA Tip" UI, and routes
 * the approval through transactions-server (EIP-712 sign + relayer
 * broadcast on Polygon Amoy). The user wallet signs typed-data only — no
 * direct broadcast, no POL required.
 *
 *   • Half 1 — drive `/auth/requests/<id>` in the browser, click
 *     "CONFIRM & SEND". The dapp handles the meta-tx.
 *   • Half 2 — `sendManaMetaTransfer` runs the same meta-tx flow in Node
 *     to return the MANA, restoring pool balance over many runs.
 *
 * Verification: assert the ERC-20 `Transfer` event in each tx's own
 * receipt — concurrency-safe; no `balanceOf` reads. The single concurrency
 * constraint that remains is "one in-flight meta-tx per EOA" (the
 * contract serializes via `nonces[user]`), enforced by `workers: 1`.
 *
 * Originator: Explorer has a `DonationsButton` on its place panel
 * (`explorer/Tests/Views/ExplorePanelSections/ExplorePanelNavmapView.cs`)
 * believed to use this same RequestPage primitive, but this spec doesn't
 * exercise Explorer — it mints the request directly against auth-api.
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

    await page.goto(withEnv(`/auth/requests/${requestId}`, dappEnvQuery()), { waitUntil: 'load' })

    // The dapp renders one of two UIs for `eth_sendTransaction`:
    //   - Generic wallet-interaction UI → [data-testid="wallet-interaction-allow-button"]
    //   - "MANA Tip" rich UI (when `to` is the MANA contract and calldata is
    //     `transfer(address,uint256)`) → no testid; button label "CONFIRM & SEND"
    // Match either; whichever appears first is the right approval control.
    const allowBtn = page
      .locator('[data-testid="wallet-interaction-allow-button"]')
      .or(page.getByRole('button', { name: /confirm\s*&\s*send/i }))
    await allowBtn.first().waitFor({ state: 'visible', timeout: 30_000 })
    await allowBtn.first().click()

    // The dapp posts the outcome (relayer-returned txHash) to auth-api as
    // soon as `/v1/transactions` resolves — it does NOT wait for the Amoy
    // receipt. Receipt waiting is done explicitly below via
    // `waitForAmoyReceipt`. Amoy receipts have been observed up to ~3 min;
    // the 240s outcome-poll timeout covers transactions-server latency
    // (sign+POST is normally well under 30s).
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

    // ─── Half 2: chain reset via meta-tx — walletB → walletA ────────────
    // Route Half 2 through transactions-server (MANA `executeMetaTransaction`
    // → relayer broadcasts on Amoy) so wallet B doesn't need POL for gas.
    // No UI replay; this is a pure off-chain sign + HTTP POST.
    const resetHash = await sendManaMetaTransfer({
      signerPrivateKey: receiver.privateKey,
      to: sender.address as `0x${string}`,
      amount: tipAmount,
      manaAddress: MANA_AMOY,
      chainId: polygonAmoy.id,
      rpcUrl: amoyRpc,
      transactionsApiUrl: `${authPairedServiceUrl('transactions-api')}/v1/transactions`
    })
    const resetReceipt = await waitForAmoyReceipt({ txHash: resetHash, rpcUrl: amoyRpc })
    assertManaTransferInReceipt(resetReceipt, {
      from: receiver.address,
      to: sender.address,
      value: tipAmount
    })
  })
})
