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
 *     `window.ethereum` (the Synpress mock) for accounts/signing.
 *
 * Must be called BEFORE the first navigation to the dapp.
 */
export async function injectAuthIdentity(
  page: Page,
  userPrivateKey: `0x${string}`,
  options: { chainId?: number } = {}
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
      connectValue,
      address,
      chainId
    }: {
      ssoKey: string
      ssoValue: string
      connectKey: string
      connectValue: string
      address: string
      chainId: number
    }) => {
      try {
        window.localStorage.setItem(ssoKey, ssoValue)
        window.localStorage.setItem(connectKey, connectValue)
      } catch {
        // localStorage may not be available on certain origins (e.g. about:blank).
      }

      // The synpress fixture installs Web3Mock + mockEthereum() at context init,
      // but mockEthereum() is called with `accounts: []`. That makes
      // `eth_requestAccounts` return [], which fails decentraland-connect's
      // auto-reconnect ("eth_requestAccounts was unsuccessful, falling back to
      // enable" → TypeError because there's no `enable` polyfill).
      //
      // Re-call Web3Mock.mock() with the actual account so the dapp's
      // `tryPreviousConnection()` succeeds on first mount. Polls briefly because
      // Web3Mock is defined by synpress's init script which runs in the same
      // execution stage but order isn't guaranteed.
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

      // Patch `window.ethereum.request` to:
      //   - polyfill legacy `enable()` (decentraland-connect's fallback path)
      //   - override `eth_chainId` and `net_version` so marketplace sees the
      //     wallet on the app's expected chain. Web3Mock's "ethereum" blockchain
      //     defaults to mainnet (0x1), but the dev marketplace targets Sepolia
      //     (0xaa36a7 / 11155111). Without this override, the dapp blocks with
      //     a "Wrong Network" modal.
      //   - answer `wallet_switchEthereumChain` so marketplace can drive
      //     mid-session chain switches (e.g. Polygon Amoy for the buy flow).
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
          // Web3Mock doesn't answer eth_getCode reliably. Marketplace calls it
          // to differentiate EOAs from smart-contract wallets (EIP-1271). Return
          // "0x" (empty bytecode = EOA) so the dapp uses the personal_sign /
          // signTypedData_v4 path instead of the contract-wallet validation
          // path, which would stall waiting for an EIP-1271 isValidSignature
          // call we can't fulfil.
          if (args.method === 'eth_getCode') return '0x'
          return original(args)
        }
        // Some libs read these properties directly instead of via request().
        try {
          ;(w.ethereum as { chainId?: string }).chainId = activeChainHex
          ;(w.ethereum as { networkVersion?: string }).networkVersion = String(parseInt(activeChainHex, 16))
        } catch {
          // Some properties may be non-writable; the request override is enough.
        }
      }, 10)
      setTimeout(() => clearInterval(ethPoll), 2_000)
    },
    { ssoKey, ssoValue, connectKey, connectValue, address, chainId }
  )

  return identity
}
