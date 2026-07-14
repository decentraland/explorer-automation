import type { TransactionReceipt } from 'viem'

// ERC721 Transfer(address indexed from, address indexed to, uint256 indexed tokenId)
const TRANSFER_EVENT_TOPIC = '0xddf252ad1be2c89b69c2b068fc378daa952ba7f163c4a11628f55a4df523b3ef'
const ZERO_TOPIC = '0x0000000000000000000000000000000000000000000000000000000000000000'

/**
 * Decodes the minted `tokenId` from an ERC-721 Transfer event in a primary-buy
 * transaction receipt. CollectionStore.buy() mints via Transfer(0x0, buyer, tokenId);
 * topics[0] is the event sig, topics[1] is `from` (zero for mints), topics[3] is
 * the indexed `tokenId`.
 *
 * Throws if no matching log is found — that's a hard signal that the receipt
 * isn't a primary mint (e.g. the test pointed at the wrong contract, or the
 * relayer broadcast a different tx than the spec expected).
 */
export function decodeMintFromReceipt(receipt: TransactionReceipt, contract: `0x${string}`): { tokenId: string } {
  const wanted = contract.toLowerCase()
  const mintLog = receipt.logs.find(
    l => l.address.toLowerCase() === wanted && l.topics[0] === TRANSFER_EVENT_TOPIC && l.topics[1] === ZERO_TOPIC
  )
  if (!mintLog || !mintLog.topics[3]) {
    throw new Error(
      `No ERC-721 mint Transfer event found for contract ${contract} in receipt ${receipt.transactionHash}`
    )
  }
  return { tokenId: BigInt(mintLog.topics[3]).toString() }
}

/**
 * Asserts that `receipt` contains an ERC-721 Transfer(from, to=`buyer`, tokenId)
 * log on `contract` — i.e. the NFT actually changed hands to the buyer. Used by
 * the secondary (accept-listing) buy to verify the on-chain ownership transfer,
 * not merely that the relayer tx "succeeded" (a receipt that only carried the
 * approval, or transferred a different token, would otherwise pass silently).
 *
 * topics[2] is the indexed `to` address (left-padded to 32 bytes); topics[3] is
 * the indexed tokenId. Throws if no matching log is found.
 */
export function assertErc721ReceivedBy(
  receipt: TransactionReceipt,
  contract: `0x${string}`,
  buyer: `0x${string}`,
  tokenId: string
): void {
  const wantedContract = contract.toLowerCase()
  const wantedBuyer = buyer.toLowerCase()
  const wantedTokenId = BigInt(tokenId)
  const transfer = receipt.logs.find(l => {
    if (l.address.toLowerCase() !== wantedContract) return false
    if (l.topics[0] !== TRANSFER_EVENT_TOPIC) return false
    const to = l.topics[2]
    const tid = l.topics[3]
    if (!to || !tid) return false
    if (`0x${to.slice(-40)}`.toLowerCase() !== wantedBuyer) return false
    return BigInt(tid) === wantedTokenId
  })
  if (!transfer) {
    throw new Error(
      `No ERC-721 Transfer of token ${tokenId} to ${buyer} on ${contract} found in receipt ${receipt.transactionHash}`
    )
  }
}
