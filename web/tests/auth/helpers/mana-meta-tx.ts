import {
  createPublicClient,
  encodeFunctionData,
  http,
  pad,
  parseSignature,
  type Address,
  type Hex
} from 'viem'
import { polygonAmoy } from 'viem/chains'
import { privateKeyToAccount } from 'viem/accounts'

/**
 * MANA `executeMetaTransaction` driver for the auth-onchain tests.
 *
 * The Decentraland MANA token on Polygon uses Matic's NativeMetaTransaction
 * pattern: the user signs an EIP-712 message off-chain, anyone (the
 * transactions-server relayer in our case) submits it through
 * `executeMetaTransaction`, and the contract pays gas. The wallet that signs
 * never spends POL.
 *
 *  - EIP-712 domain:  { name, version, verifyingContract, salt = bytes32(chainId) }
 *  - Primary type:    MetaTransaction(uint256 nonce, address from, bytes functionSignature)
 *  - Inner call:      whatever calldata the user wants the contract to execute
 *                     under their address (here: `transfer(to, value)`).
 *
 * Used by the mana-donation spec to perform the Half 2 "reset transfer"
 * without requiring POL on the receiver wallet. Half 1 goes through the
 * dapp's RequestPage UI; Half 2 is a direct POST to transactions-server.
 */
export interface SendManaMetaTransferOptions {
  /** Private key of the wallet that will sign the meta-transaction (the meta-tx `from`). */
  signerPrivateKey: Hex
  /** Recipient of the MANA transfer. */
  to: Address
  /** Amount of MANA in wei (use viem's `parseEther`). */
  amount: bigint
  /** MANA contract address on the target chain. */
  manaAddress: Address
  /** Chain id the MANA contract lives on (Polygon Amoy: 80002). */
  chainId: number
  /** RPC URL for reading the user's current MANA meta-tx nonce. */
  rpcUrl: string
  /** `https://transactions-api.decentraland.zone/v1/transactions` (or .org for prod). */
  transactionsApiUrl: string
  /** Domain `name` field. Defaults to Decentraland MANA's deployed value. */
  domainName?: string
  /** Domain `version` field. Defaults to Decentraland MANA's deployed value. */
  domainVersion?: string
}

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

const executeMetaTransactionAbi = [
  {
    name: 'executeMetaTransaction',
    type: 'function',
    stateMutability: 'payable',
    inputs: [
      { name: 'userAddress', type: 'address' },
      { name: 'functionSignature', type: 'bytes' },
      { name: 'sigR', type: 'bytes32' },
      { name: 'sigS', type: 'bytes32' },
      { name: 'sigV', type: 'uint8' }
    ],
    outputs: [{ type: 'bytes' }]
  }
] as const

const getNonceAbi = [
  {
    name: 'getNonce',
    type: 'function',
    stateMutability: 'view',
    inputs: [{ name: 'user', type: 'address' }],
    outputs: [{ type: 'uint256' }]
  }
] as const

/**
 * Signs + relays a MANA `transfer(to, amount)` as a meta-transaction. Returns
 * the relayer-broadcast `txHash` (Polygon Amoy). The caller is responsible
 * for waiting for the receipt — use `waitForAmoyReceipt`.
 */
export async function sendManaMetaTransfer(options: SendManaMetaTransferOptions): Promise<Hex> {
  const {
    signerPrivateKey,
    to,
    amount,
    manaAddress,
    chainId,
    rpcUrl,
    transactionsApiUrl,
    // Matches the deployed Decentraland MANA on Polygon Amoy
    // (`name()` returns "Decentraland MANA(PoS)" — the (PoS) suffix is part
    // of the EIP-712 domain hash; mainnet Polygon MANA uses the same name).
    domainName = 'Decentraland MANA(PoS)',
    domainVersion = '1'
  } = options

  const account = privateKeyToAccount(signerPrivateKey)
  const pub = createPublicClient({ chain: polygonAmoy, transport: http(rpcUrl) })

  const nonce = await pub.readContract({
    address: manaAddress,
    abi: getNonceAbi,
    functionName: 'getNonce',
    args: [account.address]
  })

  const functionSignature = encodeFunctionData({
    abi: erc20TransferAbi,
    functionName: 'transfer',
    args: [to, amount]
  })

  // Matic NativeMetaTransaction EIP-712 schema, as deployed for Decentraland
  // MANA(PoS) on Polygon Amoy. Two non-obvious bits, both of which produce
  // a "Signer and signature do not match" revert if wrong:
  //
  //   1. Domain typehash is salt-based, NOT chainId-based:
  //      EIP712Domain(string name,string version,address verifyingContract,bytes32 salt)
  //      where `salt` = bytes32(chainId).
  //   2. MetaTransaction typehash uses `bytes functionSignature` (NOT bytes32).
  //      viem hashes the bytes value automatically inside the struct hash.
  //
  // Verified against the live contract: `eth_call(executeMetaTransaction)`
  // succeeds with this schema and reverts with `bytes32`.
  const signature = await account.signTypedData({
    domain: {
      name: domainName,
      version: domainVersion,
      verifyingContract: manaAddress,
      salt: pad(`0x${chainId.toString(16)}`, { size: 32 })
    },
    types: {
      MetaTransaction: [
        { name: 'nonce', type: 'uint256' },
        { name: 'from', type: 'address' },
        { name: 'functionSignature', type: 'bytes' }
      ]
    },
    primaryType: 'MetaTransaction',
    message: {
      nonce,
      from: account.address,
      functionSignature
    }
  })

  // `parseSignature` returns either `v` (27|28 as bigint) or `yParity` (0|1).
  // executeMetaTransaction wants `uint8 sigV` in the 27/28 form.
  const parsed = parseSignature(signature)
  const sigV: number =
    parsed.v !== undefined ? Number(parsed.v) : parsed.yParity !== undefined ? parsed.yParity + 27 : 0
  if (sigV !== 27 && sigV !== 28) {
    throw new Error(`Unexpected EIP-712 signature recovery byte: ${sigV} (signature=${signature})`)
  }

  const metaTxCalldata = encodeFunctionData({
    abi: executeMetaTransactionAbi,
    functionName: 'executeMetaTransaction',
    args: [account.address, functionSignature, parsed.r, parsed.s, sigV]
  })

  const res = await fetch(transactionsApiUrl, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      transactionData: {
        from: account.address.toLowerCase(),
        params: [manaAddress.toLowerCase(), metaTxCalldata]
      }
    })
  })

  if (!res.ok) {
    throw new Error(`transactions-server responded ${res.status}: ${await res.text()}`)
  }
  const body = (await res.json()) as { txHash?: string; data?: { txHash?: string } }
  const txHash = (body.txHash ?? body.data?.txHash) as Hex | undefined
  if (!txHash || !/^0x[0-9a-f]{64}$/i.test(txHash)) {
    throw new Error(`transactions-server response missing txHash: ${JSON.stringify(body)}`)
  }

  return txHash
}
