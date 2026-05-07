import type { Page } from '@playwright/test'
import {
  createPublicClient,
  createWalletClient,
  http,
  type Chain,
  type Hex,
  type PublicClient,
  type WalletClient
} from 'viem'
import { privateKeyToAccount } from 'viem/accounts'
import { polygonAmoy, sepolia } from 'viem/chains'

export interface BroadcastWalletOptions {
  privateKey: `0x${string}`
  /** Map chainId → RPC URL. The wallet honours `wallet_switchEthereumChain`. */
  rpcUrls: Record<number, string>
  /** Initial chain id. The dapp may switch after auth. */
  initialChainId: number
}

const SUPPORTED_CHAINS: Record<number, Chain> = {
  [polygonAmoy.id]: polygonAmoy,
  [sepolia.id]: sepolia
}

/**
 * Adds transaction-broadcasting interceptors on top of an already-authenticated
 * page (call AFTER `injectAuthIdentity`). Intercepts:
 *
 *  - `eth_sendTransaction`     → viem walletClient.sendTransaction(...) on the active chain
 *  - `eth_signTypedData_v4`    → viem account.signTypedData(...) (for meta-tx flows)
 *  - `wallet_switchEthereumChain` → re-bind walletClient to the new chain
 *
 * Must be called BEFORE the first navigation. Adds an init script that runs
 * on every page load.
 */
export async function setupBroadcastWallet(page: Page, options: BroadcastWalletOptions): Promise<void> {
  const { privateKey, rpcUrls, initialChainId } = options

  const account = privateKeyToAccount(privateKey)

  const buildClients = (chainId: number): { wallet: WalletClient; pub: PublicClient } => {
    const chain = SUPPORTED_CHAINS[chainId]
    if (!chain) throw new Error(`Unsupported chain ${chainId}`)
    const rpcUrl = rpcUrls[chainId]
    if (!rpcUrl) throw new Error(`No RPC URL configured for chain ${chainId}`)
    return {
      wallet: createWalletClient({ account, chain, transport: http(rpcUrl) }),
      pub: createPublicClient({ chain, transport: http(rpcUrl) })
    }
  }

  let activeChainId = initialChainId
  let { wallet, pub } = buildClients(activeChainId)

  await page.exposeFunction('__sendTransaction', async (tx: Record<string, unknown>): Promise<Hex> => {
    const hash = await wallet.sendTransaction(tx as Parameters<typeof wallet.sendTransaction>[0])
    await pub.waitForTransactionReceipt({ hash })
    return hash
  })

  await page.exposeFunction(
    '__signTypedData',
    async (params: { domain: unknown; types: unknown; primaryType: string; message: unknown }): Promise<Hex> => {
      return account.signTypedData(params as Parameters<typeof account.signTypedData>[0])
    }
  )

  await page.exposeFunction('__switchChain', async (chainIdHex: string): Promise<null> => {
    activeChainId = Number.parseInt(chainIdHex, 16)
    ;({ wallet, pub } = buildClients(activeChainId))
    return null
  })

  // Forward read methods to the active chain's RPC. Web3Mock errors on
  // unmocked calls (`Please mock the request to: 0x...`), and the dapp's
  // approval-check / metadata flows do plenty of these (e.g. ERC721
  // `isApprovedForAll`, `ownerOf`, ERC20 `allowance`). Routing through the
  // viem public client keeps the wallet mock for write/sign operations only.
  await page.exposeFunction('__ethRead', async (method: string, params: unknown[]): Promise<unknown> => {
    return pub.request({ method, params } as Parameters<PublicClient['request']>[0])
  })

  await page.addInitScript(() => {
    // Wait for window.ethereum (synpress's mockEthereum installs it),
    // then layer broadcast interception on top of whatever request handler
    // is currently bound (e.g. injectAuthIdentity's chain-id override).
    const interval = setInterval(() => {
      const w = window as unknown as {
        ethereum?: { request: (a: { method: string; params?: unknown[] }) => Promise<unknown> }
        __sendTransaction: (tx: unknown) => Promise<string>
        __signTypedData: (p: unknown) => Promise<string>
        __switchChain: (cid: string) => Promise<null>
        __ethRead: (method: string, params: unknown[]) => Promise<unknown>
        __broadcastWalletInstalled?: boolean
      }
      if (!w.ethereum) return
      if (w.__broadcastWalletInstalled) {
        clearInterval(interval)
        return
      }
      // Read methods we forward to the real RPC instead of letting Web3Mock
      // attempt to handle them (it errors on anything not pre-mocked). Keep
      // this list narrow to avoid surprising overrides of methods auth-identity
      // already handles (e.g. eth_chainId, eth_getCode).
      const READ_METHODS = new Set([
        'eth_call',
        'eth_getBalance',
        'eth_getTransactionCount',
        'eth_estimateGas',
        'eth_getTransactionByHash',
        'eth_getTransactionReceipt',
        'eth_getLogs',
        'eth_blockNumber',
        'eth_getBlockByNumber',
        'eth_getBlockByHash'
      ])

      const original = w.ethereum.request.bind(w.ethereum)
      w.ethereum.request = async args => {
        if (args.method === 'eth_sendTransaction' && Array.isArray(args.params)) {
          return w.__sendTransaction(args.params[0])
        }
        if (args.method === 'eth_signTypedData_v4' && Array.isArray(args.params)) {
          const payload = args.params[1]
          const parsed = typeof payload === 'string' ? JSON.parse(payload) : payload
          return w.__signTypedData(parsed)
        }
        if (args.method === 'wallet_switchEthereumChain' && Array.isArray(args.params)) {
          const param = args.params[0] as { chainId: string }
          await w.__switchChain(param.chainId)
          return original(args)
        }
        if (READ_METHODS.has(args.method)) {
          return w.__ethRead(args.method, (args.params as unknown[]) ?? [])
        }
        return original(args)
      }
      w.__broadcastWalletInstalled = true
      clearInterval(interval)
    }, 10)
    setTimeout(() => clearInterval(interval), 5_000)
  })
}
