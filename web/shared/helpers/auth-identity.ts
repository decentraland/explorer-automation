import type { Page } from '@playwright/test'
import { generatePrivateKey, privateKeyToAccount, privateKeyToAddress } from 'viem/accounts'

/**
 * Decentraland's ephemeral message format — must match exactly what
 * `@dcl/crypto`'s `Authenticator.getEphemeralMessage` produces, otherwise the
 * server-side recovery of the signature won't match the user's address.
 */
function ephemeralMessage(ephemeralAddress: string, expiration: Date): string {
  return `Decentraland Login\nEphemeral address: ${ephemeralAddress}\nExpiration: ${expiration.toISOString()}`
}

export interface AuthIdentity {
  ephemeralIdentity: { privateKey: string; publicKey: string; address: string }
  expiration: Date
  authChain: Array<{ type: 'SIGNER' | 'ECDSA_EPHEMERAL'; payload: string; signature: string }>
}

/**
 * Builds a valid AuthIdentity signed by the given user wallet, ready to be
 * persisted into the SSO client's localStorage. Mirrors what the
 * `decentraland.org/auth` dapp produces on a real sign-in.
 */
export async function buildAuthIdentity(
  userPrivateKey: `0x${string}`,
  ephemeralMinutesDuration = 60 * 24 * 30
): Promise<AuthIdentity> {
  const userAccount = privateKeyToAccount(userPrivateKey)
  const ephemeralPriv = generatePrivateKey()
  const ephemeralAccount = privateKeyToAccount(ephemeralPriv)

  const expiration = new Date()
  expiration.setMinutes(expiration.getMinutes() + ephemeralMinutesDuration)

  const message = ephemeralMessage(ephemeralAccount.address, expiration)
  const signature = await userAccount.signMessage({ message })

  return {
    ephemeralIdentity: {
      privateKey: ephemeralPriv,
      // Public key is not used by the SSO consumer, so an empty string is fine.
      publicKey: '',
      address: ephemeralAccount.address
    },
    expiration,
    authChain: [
      { type: 'SIGNER', payload: userAccount.address, signature: '' },
      { type: 'ECDSA_EPHEMERAL', payload: message, signature }
    ]
  }
}

export interface InjectAuthIdentityOptions {
  /** App chain id seeded into decentraland-connect-storage-key. Default Sepolia (11155111). */
  chainId?: number
}

/**
 * Pre-seeds the page's origin with two localStorage entries that together
 * make a Decentraland dapp treat the wallet as already authenticated on
 * first load:
 *
 *  1. `single-sign-on-<addr>` — the SSO client's identity blob. The client
 *     tries the iframe first; if iframe comms fail it falls back to reading
 *     this key (see `@dcl/single-sign-on-client/dist/SingleSignOn.js:56`).
 *
 *  2. `decentraland-connect-storage-key` — `decentraland-connect`'s
 *     `tryPreviousConnection()` reads this on mount and reconnects via the
 *     persisted `providerType`. With `providerType: "injected"` it uses
 *     `window.ethereum` (whatever wallet is bound to that name) for accounts
 *     and signing.
 *
 * This function is wallet-agnostic — it only sets up the dapp-side identity
 * contract. To actually back `window.ethereum` with the Synpress + Web3Mock
 * stack used by these tests, also call `installInjectedWalletMock` after
 * this. Real wallet integrations (WalletConnect, real MetaMask) skip the
 * mock helper.
 *
 * Must be called BEFORE the first navigation to the dapp.
 */
export async function injectAuthIdentity(
  page: Page,
  userPrivateKey: `0x${string}`,
  options: InjectAuthIdentityOptions = {}
): Promise<AuthIdentity> {
  const identity = await buildAuthIdentity(userPrivateKey)
  const address = privateKeyToAddress(userPrivateKey).toLowerCase()
  // Default chain: Ethereum Sepolia. Marketplace's CHAIN_ID for the dev env
  // is 11155111 (Sepolia); Polygon Amoy is the secondary network for MANA.
  // For the wallet provider's auto-connect we need the app-chain id.
  const chainId = options.chainId ?? 11155111
  const ssoKey = `single-sign-on-${address}`
  const ssoValue = JSON.stringify({
    ...identity,
    expiration: identity.expiration.toISOString()
  })
  const connectKey = 'decentraland-connect-storage-key'
  const connectValue = JSON.stringify({ providerType: 'injected', chainId })

  await page.addInitScript(
    ({
      ssoKey,
      ssoValue,
      connectKey,
      connectValue
    }: {
      ssoKey: string
      ssoValue: string
      connectKey: string
      connectValue: string
    }) => {
      try {
        window.localStorage.setItem(ssoKey, ssoValue)
        window.localStorage.setItem(connectKey, connectValue)
      } catch {
        // localStorage may not be available on certain origins (e.g. about:blank).
      }
    },
    { ssoKey, ssoValue, connectKey, connectValue }
  )

  return identity
}

export interface InstallInjectedWalletMockOptions {
  /** Initial chain id reported via eth_chainId / net_version. Default Sepolia (11155111). */
  chainId?: number
}

/**
 * Patches the Synpress + Web3Mock injected-wallet stack so the dapp sees a
 * connected EOA on the expected chain. ONLY needed for tests that use
 * `@synthetixio/ethereum-wallet-mock`-backed wallets — real-wallet
 * integrations (WalletConnect, real MetaMask) must NOT call this.
 *
 * Two responsibilities:
 *
 *  1. **Web3Mock account override.** Synpress's fixture calls `mockEthereum()`
 *     with `accounts: []`, so `eth_requestAccounts` returns []. Re-call
 *     `Web3Mock.mock()` with the test's actual address so
 *     decentraland-connect's `tryPreviousConnection()` succeeds on first
 *     mount.
 *
 *  2. **`window.ethereum.request` override.** Patches the request handler to:
 *     - polyfill legacy `enable()` (decentraland-connect's fallback path)
 *     - answer `eth_chainId` / `net_version` with the configured chain id
 *       (Web3Mock defaults to mainnet 0x1; dev marketplace expects Sepolia)
 *     - answer `wallet_switchEthereumChain` so the dapp can drive mid-session
 *       chain switches (e.g. into Polygon Amoy for the buy flow)
 *     - answer `eth_getCode` with `0x` (EOA bytecode) so the dapp picks the
 *       personal_sign / signTypedData_v4 path instead of EIP-1271 contract-
 *       wallet validation, which Web3Mock can't fulfil.
 *
 * Must be called BEFORE the first navigation to the dapp.
 */
export async function installInjectedWalletMock(
  page: Page,
  userPrivateKey: `0x${string}`,
  options: InstallInjectedWalletMockOptions = {}
): Promise<void> {
  const address = privateKeyToAddress(userPrivateKey).toLowerCase()
  const chainId = options.chainId ?? 11155111

  await page.addInitScript(
    ({ address, chainId }: { address: string; chainId: number }) => {
      // Web3Mock account override.
      const remock = () => {
        const w = window as unknown as { Web3Mock?: { mock: (cfg: unknown) => unknown } }
        if (!w.Web3Mock) return false
        w.Web3Mock.mock({
          blockchain: 'ethereum',
          wallet: 'metamask',
          accounts: { return: [address] }
        })
        return true
      }
      if (!remock()) {
        const interval = setInterval(() => {
          if (remock()) clearInterval(interval)
        }, 10)
        setTimeout(() => clearInterval(interval), 2_000)
      }

      // window.ethereum.request override.
      const chainIdHex = `0x${chainId.toString(16)}`
      let activeChainHex = chainIdHex
      const ethPoll = setInterval(() => {
        const w = window as unknown as {
          ethereum?: {
            enable?: () => Promise<unknown>
            request: (a: { method: string; params?: unknown[] }) => Promise<unknown>
            chainId?: string
            networkVersion?: string
          }
        }
        if (!w.ethereum) return
        clearInterval(ethPoll)
        if (!w.ethereum.enable) {
          w.ethereum.enable = () => w.ethereum!.request({ method: 'eth_requestAccounts' })
        }
        const original = w.ethereum.request.bind(w.ethereum)
        w.ethereum.request = async args => {
          if (args.method === 'eth_chainId') return activeChainHex
          if (args.method === 'net_version') return String(parseInt(activeChainHex, 16))
          if (args.method === 'wallet_switchEthereumChain' && Array.isArray(args.params)) {
            const param = args.params[0] as { chainId?: string }
            if (param?.chainId) activeChainHex = param.chainId
            return null
          }
          if (args.method === 'eth_getCode') return '0x'
          return original(args)
        }
        try {
          ;(w.ethereum as { chainId?: string }).chainId = activeChainHex
          ;(w.ethereum as { networkVersion?: string }).networkVersion = String(parseInt(activeChainHex, 16))
        } catch {
          // Some properties may be non-writable; the request override is enough.
        }
      }, 10)
      setTimeout(() => clearInterval(ethPoll), 2_000)
    },
    { address, chainId }
  )
}
