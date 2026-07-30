import { privateKeyToAccount } from 'viem/accounts'
import type { Page, Route } from '@playwright/test'
import type { EthereumWalletMock } from '@synthetixio/ethereum-wallet-mock/playwright'
import { getBaseUrl } from '../../../shared/helpers/env.js'

/**
 * Intercepts every Decentraland catalyst `/lambdas/profiles/<address>` call
 * and returns "Profile not found", so the auth dapp's `useEnsureProfile`
 * predicate (`profile.avatars[0].name !== undefined`) evaluates false and
 * routes the user to `/auth/quick-setup` instead of the homepage.
 *
 * Without this, even a freshly-generated wallet often hits the homepage
 * because catalysts return placeholder/default profiles for unknown addresses.
 *
 * Call BEFORE clicking the MetaMask button. Skip this for "recurrent user"
 * tests where you WANT the dapp to find a profile.
 *
 * Returns an unmock function that removes the interception, useful for
 * register-then-recurrent flows that switch behavior mid-test.
 */
export async function mockNoProfileOnCatalysts(page: Page): Promise<() => Promise<void>> {
  // Only intercept lookups by 0x-address. The dapp also fetches `defaultN`
  // pseudo-profiles on quick-setup to populate the "randomize" presets;
  // intercepting those would break the avatar picker UI.
  const matcher = (url: URL): boolean => /\/lambdas\/profiles?\/0x[a-f0-9]{40}\b/i.test(url.toString())
  const handler = (route: Route): Promise<void> =>
    route.fulfill({
      status: 404,
      contentType: 'application/json',
      body: JSON.stringify({ error: 'Not Found', message: 'Profile not found' })
    })
  await page.route(matcher, handler)
  return async () => {
    await page.unroute(matcher, handler)
  }
}

// Pages that have had the one-time wallet plumbing installed. Lets
// `setupMockedWallet` be safely re-called on the same page (e.g. for a
// register-then-recurrent flow) — it skips re-exposing `__signMessage` and
// re-adding the polyfill init script, both of which would otherwise error or
// stack.
const installedOn = new WeakSet<Page>()

/**
 * Sets up a mocked Ethereum wallet on the page, navigates to `/auth/login`,
 * and configures Web3Mock to advertise the given private key's address. After
 * this, clicking the MetaMask button on the auth screen drives the full
 * sign-in flow with real `viem`-backed signatures.
 *
 * Idempotent on the same page — calling twice (e.g. to re-login as a recurrent
 * user) re-navigates and re-binds the mock state, but only installs the
 * one-time plumbing (polyfill + signer binding) on the first call. The signer
 * is closure-bound to the FIRST call's privateKey, so use the same key across
 * calls (the recurrent flow does, by design).
 */
export async function setupMockedWallet(
  page: Page,
  ethereumWalletMock: EthereumWalletMock,
  { redirectTo, privateKey }: { redirectTo?: string; privateKey: `0x${string}` }
): Promise<void> {
  const account = privateKeyToAccount(privateKey)

  if (!installedOn.has(page)) {
    // Polyfill legacy `window.ethereum.enable()` (used by some wallet detection
    // libraries the dapp depends on).
    await page.context().addInitScript(() => {
      const interval = setInterval(() => {
        const w = window as unknown as {
          ethereum?: {
            enable?: () => Promise<unknown>
            request: (a: { method: string }) => Promise<unknown>
          }
        }
        if (w.ethereum && !w.ethereum.enable) {
          w.ethereum.enable = () => w.ethereum!.request({ method: 'eth_requestAccounts' })
          clearInterval(interval)
        }
      }, 50)
    })

    await page.exposeFunction('__signMessage', async (hex: string) => {
      const message = Buffer.from(hex.slice(2), 'hex').toString('utf-8')
      return account.signMessage({ message })
    })

    installedOn.add(page)
  }

  const url = new URL(`${getBaseUrl()}/auth/login`)
  if (redirectTo) url.searchParams.set('redirectTo', redirectTo)
  await page.goto(url.toString(), { waitUntil: 'load' })

  await ethereumWalletMock.connectToDapp()
  await ethereumWalletMock.importWalletFromPrivateKey(privateKey)
  await applyPersonalSignOverride(page)
}

/**
 * Stubs `navigator.gpu` so the dapp's `/auth/avatar-setup` page passes its
 * internal WebGPU guard (`'gpu' in navigator && !!await navigator.gpu.requestAdapter()`)
 * without depending on the host's actual GPU. Headed Chrome on macOS often
 * returns a null adapter from `requestAdapter()` even with real hardware,
 * which would bounce the page to `/setup`. The stub returns a non-null
 * placeholder so the guard's truthiness check is satisfied.
 *
 * Use BEFORE navigating to `/auth/avatar-setup`. Has no effect on other
 * dapp routes — the stub is harmless if Web3Mock or other features ignore it.
 */
export async function stubNavigatorGpu(page: Page): Promise<void> {
  await page.addInitScript(() => {
    const w = window as unknown as { navigator: Navigator }
    if (!('gpu' in w.navigator)) {
      Object.defineProperty(w.navigator, 'gpu', {
        configurable: true,
        // The dapp only checks `!!adapter`, not its API surface, so an empty
        // object is enough.
        value: { requestAdapter: async () => ({}) }
      })
    }
  })
}

/**
 * Adds a context-level init script that auto-mocks Web3Mock with the given
 * address as soon as it appears on any subsequent navigation. Use this BEFORE
 * navigating to dapp routes that probe `window.ethereum` immediately on page
 * load (e.g. `/auth/requests/<id>`) — without it, the dapp may decide the
 * user isn't logged in before `rebindWalletMock` has a chance to fire.
 *
 * Assumes `setupMockedWallet` has already run on the page (the polyfill init
 * script + __signMessage exposeFunction are still active across navigations).
 */
export async function installAutoWalletMockInitScript(page: Page, address: string): Promise<void> {
  await page.context().addInitScript(addr => {
    const interval = setInterval(() => {
      const w = window as unknown as {
        Web3Mock?: { mock: (cfg: unknown) => unknown }
      }
      if (w.Web3Mock) {
        w.Web3Mock.mock({
          blockchain: 'ethereum',
          wallet: 'metamask',
          accounts: { return: [addr] }
        })
        clearInterval(interval)
      }
    }, 10)
  }, address)
}

/**
 * Patches `window.ethereum.request` so `personal_sign` requests are routed
 * to our Node-side viem signer (`__signMessage`) instead of the mock's stub.
 * Other RPC methods pass through to the original handler.
 *
 * Use after any `page.goto` (which wipes JS state) when the new page needs
 * to sign with a real address-matching signature. Assumes `setupMockedWallet`
 * has run at least once on this page (so `__signMessage` is exposed).
 */
export async function applyPersonalSignOverride(page: Page): Promise<void> {
  await page.evaluate(() => {
    type Eth = { request: (args: { method: string; params?: unknown[] }) => Promise<unknown> }
    const w = window as unknown as {
      ethereum: Eth
      __signMessage: (hex: string) => Promise<string>
    }
    const original = w.ethereum.request.bind(w.ethereum)
    w.ethereum.request = async args => {
      if (args.method === 'personal_sign' && Array.isArray(args.params) && typeof args.params[0] === 'string') {
        return w.__signMessage(args.params[0])
      }
      return original(args)
    }
  })
}

/**
 * Installs the personal_sign override as an init script so it takes effect
 * BEFORE page JS executes. This avoids the race where the auth dapp requests
 * `personal_sign` between load completion and a post-navigation
 * `page.evaluate()` call.
 *
 * Polls for `window.ethereum` and `window.__signMessage` (exposed by
 * `setupMockedWallet`) then patches `request` in-place. Must be called
 * BEFORE `page.goto()` — `addInitScript` only fires on subsequent navigations.
 */
export async function installPersonalSignOverrideInitScript(page: Page): Promise<void> {
  await page.addInitScript(() => {
    const poll = setInterval(() => {
      const w = window as unknown as {
        ethereum?: { request: (args: { method: string; params?: unknown[] }) => Promise<unknown> }
        __signMessage?: (hex: string) => Promise<string>
      }
      if (w.ethereum?.request && w.__signMessage) {
        const original = w.ethereum.request.bind(w.ethereum)
        const signMsg = w.__signMessage
        w.ethereum.request = async (args: { method: string; params?: unknown[] }) => {
          if (args.method === 'personal_sign' && Array.isArray(args.params) && typeof args.params[0] === 'string') {
            return signMsg(args.params[0])
          }
          return original(args)
        }
        clearInterval(poll)
      }
    }, 10)
  })
}

/**
 * Methods that make the wallet produce a signature or broadcast.
 * Lowercased — compare against a lowercased method name.
 *
 * `dcl_personal_sign` is not an EIP-1193 method and no wallet implements it,
 * but it belongs here: it is the auth server's own request method, and the
 * most likely way the retired sign-in regresses is the request page forwarding
 * that method verbatim to `window.ethereum.request`. Leaving it out let such a
 * call be recorded and then filtered away before the assertion.
 */
export const SIGNING_RPC_METHODS = new Set([
  'personal_sign',
  'dcl_personal_sign',
  'eth_sign',
  'eth_signtypeddata',
  'eth_signtypeddata_v3',
  'eth_signtypeddata_v4',
  'eth_sendtransaction'
])

/**
 * Strictly passive wallet reads: they return existing state and cannot prompt
 * the user, request permission, or change anything. Lowercased.
 *
 * This is the allowlist counterpart to {@link SIGNING_RPC_METHODS}. A denylist
 * can only reject what it already knows about, which is how `dcl_personal_sign`
 * slipped through; a spec asserting "this flow must not touch the wallet" gets
 * a stronger guarantee by rejecting everything outside this set, so a method
 * nobody thought to enumerate fails the test instead of passing it.
 *
 * Keep the bar at *passive*, not merely *non-signing*. `eth_requestAccounts`
 * and `wallet_requestPermissions` are deliberately absent: both initiate a
 * connection or permission prompt, which is the wallet being reached even
 * though no signature is produced. Adding a prompting method here would let a
 * flow that must not touch the wallet at all pass silently.
 */
export const READ_ONLY_RPC_METHODS = new Set([
  'eth_accounts',
  'eth_chainid',
  'net_version',
  'wallet_getpermissions',
  'eth_getbalance',
  'eth_blocknumber',
  'eth_call'
])

/**
 * Records every `window.ethereum.request` method the page invokes, so a spec
 * can assert positively that a flow never reached the wallet instead of
 * inferring it from an absent outcome (an outcome can also be absent because
 * the wallet was called and failed locally).
 *
 * Install BEFORE the `page.goto` under test — this registers an init script,
 * so it only covers subsequent navigations. Returns a reader for the recorded
 * method names, in call order; filter with `SIGNING_RPC_METHODS` for the
 * signing subset.
 *
 * Attachment is **synchronous**, via accessors installed at document_start:
 * one on `window.ethereum` so a provider assigned at any later point is
 * instrumented in the same tick as its assignment, and one on the provider's
 * own `request` so a reassignment (Web3Mock re-mocking, an override) is
 * re-wrapped the same way. A provider already present at document_start is
 * wrapped outright. This matters because the RequestPage probes wallet state
 * during page load: an attach that only ran on a timer could miss a call made
 * between the provider appearing and the next tick — exactly the regression
 * this spy exists to catch. The interval below is a fallback for the one case
 * accessors can't cover (something redefining the property with its own
 * descriptor), never the primary mechanism.
 *
 * Don't pair this with `applyPersonalSignOverride` on the flow being watched:
 * that override replaces `request` with a wrapper that answers `personal_sign`
 * itself, so the very call worth catching would never reach the spy underneath
 * it.
 */
export async function installWalletRpcSpy(page: Page): Promise<() => Promise<string[]>> {
  await page.addInitScript(() => {
    type Args = { method: string; params?: unknown[] }
    type Request = ((args: Args) => Promise<unknown>) & { __spied?: boolean }
    type Provider = { request: Request }
    type SpiedGetter = (() => Request) & { __spiedAccessor?: boolean }

    const w = window as unknown as { ethereum?: Provider; __walletRpcCalls?: string[] }
    const calls: string[] = []
    w.__walletRpcCalls = calls

    const wrap = (fn: Request, provider: Provider): Request => {
      if (typeof fn !== 'function' || fn.__spied) return fn
      const spied: Request = async args => {
        if (args && typeof args.method === 'string') calls.push(args.method)
        return fn.call(provider, args)
      }
      spied.__spied = true
      return spied
    }

    const instrument = (provider: Provider | undefined): Provider | undefined => {
      if (!provider || typeof provider !== 'object') return provider
      const descriptor = Object.getOwnPropertyDescriptor(provider, 'request')
      if ((descriptor?.get as SpiedGetter | undefined)?.__spiedAccessor) return provider

      let current = wrap(provider.request, provider)
      const get: SpiedGetter = () => current
      get.__spiedAccessor = true
      try {
        Object.defineProperty(provider, 'request', {
          configurable: true,
          enumerable: true,
          get,
          set: (next: Request) => {
            current = wrap(next, provider)
          }
        })
      } catch {
        // `request` is locked down — best effort, then leave it to the sweep.
        try {
          provider.request = current
        } catch {
          /* nothing else to try */
        }
      }
      return provider
    }

    let providerRef = instrument(w.ethereum)
    try {
      Object.defineProperty(window, 'ethereum', {
        configurable: true,
        enumerable: true,
        get: () => providerRef,
        set: (next: Provider) => {
          providerRef = instrument(next)
        }
      })
    } catch {
      /* the property refused redefinition — the sweep is the fallback */
    }

    // Fallback only: re-instrument if either accessor is clobbered by code that
    // redefines the property with its own descriptor.
    setInterval(() => {
      const provider = w.ethereum
      if (provider && !provider.request?.__spied) instrument(provider)
    }, 10)
  })

  return async () => page.evaluate(() => (window as unknown as { __walletRpcCalls?: string[] }).__walletRpcCalls ?? [])
}

/**
 * Heavier rebind: re-runs the synpress `connectToDapp` + `importWalletFromPrivateKey`
 * sequence on the current page in addition to applying the personal_sign
 * override. Use this when you need the wallet mock fully re-bootstrapped after
 * navigation (e.g. cross-site nav). Avoid for routes that probe wallet state
 * mid-load (e.g. `/auth/requests/<id>`) — the synpress calls re-introduce the
 * default address mid-handshake and can crash signing flows; use
 * `installAutoWalletMockInitScript` + `installPersonalSignOverrideInitScript` instead.
 */
export async function rebindWalletMock(
  page: Page,
  ethereumWalletMock: EthereumWalletMock,
  privateKey: `0x${string}`
): Promise<void> {
  await ethereumWalletMock.connectToDapp()
  await ethereumWalletMock.importWalletFromPrivateKey(privateKey)
  await applyPersonalSignOverride(page)
}
