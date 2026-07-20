import { randomUUID } from 'node:crypto'
import type { Page } from '@playwright/test'
import { authServerUrl } from './auth-server.js'

// ─── URL construction ───────────────────────────────────────────────────────

export function generateAuthRequestId(): string {
  return randomUUID()
}

/**
 * Builds the request-page URL the Explorer opens for deeplink auth.
 *
 * Format: `/auth/requests/{authRequestId}?flow=deeplink`
 *
 * The route UUID is the client-generated correlation ID — forwarded to the
 * client as the deep link's `authRequestId` so it can match this login to the
 * instance that requested it. `flow=deeplink` opts into the deep-link login
 * handoff (case-insensitive on the auth dapp side).
 */
export function buildDeeplinkLoginPath(authRequestId: string): string {
  return `/auth/requests/${authRequestId}?flow=deeplink`
}

// ─── Deep link redirect capture ─────────────────────────────────────────────

/**
 * Installs an init script that intercepts `decentraland://` navigation
 * attempts before they reach the browser (which would fail with
 * ERR_UNKNOWN_URL_SCHEME). The captured URL is stored on `window` and
 * retrievable via {@link getCapturedDeepLink}.
 *
 * The auth dapp launches the deep link via a hidden iframe (`iframe.src = url`)
 * so the primary interceptor patches the HTMLIFrameElement `src` setter.
 * Location and window.open interceptors are kept as fallbacks.
 *
 * Must be called BEFORE navigating to the auth page — `addInitScript` only
 * takes effect on subsequent navigations.
 */
export async function installDeeplinkCapture(page: Page): Promise<void> {
  await page.addInitScript(() => {
    const w = window as unknown as { __capturedDeepLink: string | null }
    w.__capturedDeepLink = null

    // Primary: intercept HTMLIFrameElement.src setter — the auth dapp creates
    // a hidden iframe with `iframe.src = 'decentraland://...'` to trigger the
    // OS protocol handler without a visible navigation.
    const srcDesc = Object.getOwnPropertyDescriptor(HTMLIFrameElement.prototype, 'src')
    if (srcDesc?.set) {
      const origSrcSet = srcDesc.set
      Object.defineProperty(HTMLIFrameElement.prototype, 'src', {
        ...srcDesc,
        set(this: HTMLIFrameElement, value: string) {
          if (typeof value === 'string' && value.startsWith('decentraland://')) {
            w.__capturedDeepLink = value
            return
          }
          origSrcSet.call(this, value)
        }
      })
    }

    // Fallback: Location.prototype.assign
    const origAssign = Location.prototype.assign
    Location.prototype.assign = function (url: string | URL) {
      const str = typeof url === 'string' ? url : url.toString()
      if (str.startsWith('decentraland://')) {
        w.__capturedDeepLink = str
        return
      }
      return origAssign.call(this, url)
    }

    // Fallback: Location.prototype.replace
    const origReplace = Location.prototype.replace
    Location.prototype.replace = function (url: string | URL) {
      const str = typeof url === 'string' ? url : url.toString()
      if (str.startsWith('decentraland://')) {
        w.__capturedDeepLink = str
        return
      }
      return origReplace.call(this, url)
    }

    // Fallback: window.open
    const origOpen = window.open.bind(window)
    window.open = function (url?: string | URL, target?: string, features?: string): WindowProxy | null {
      const str = typeof url === 'string' ? url : (url?.toString() ?? '')
      if (str.startsWith('decentraland://')) {
        w.__capturedDeepLink = str
        return null
      }
      return origOpen(url, target, features)
    }
  })
}

/**
 * Retrieves the `decentraland://` URL captured by {@link installDeeplinkCapture},
 * or `null` if no redirect has been attempted yet.
 */
export async function getCapturedDeepLink(page: Page): Promise<string | null> {
  return page.evaluate(() => (window as unknown as { __capturedDeepLink: string | null }).__capturedDeepLink)
}

/**
 * Polls until the deep link redirect is captured or the timeout elapses.
 * Returns the full `decentraland://` URL.
 */
export async function waitForDeepLinkRedirect(page: Page, timeoutMs = 60_000): Promise<string> {
  const deadline = Date.now() + timeoutMs
  while (Date.now() < deadline) {
    const url = await getCapturedDeepLink(page)
    if (url) return url
    await page.waitForTimeout(500)
  }
  throw new Error(`Deep link redirect was not captured within ${timeoutMs / 1000}s`)
}

// ─── Deep link URL parsing ──────────────────────────────────────────────────

export interface DeepLinkParams {
  signin: string | null
  authRequestId: string | null
}

/**
 * Parses the query parameters from a `decentraland://` deep link URL.
 * Extracts `signin` (the identity ID) and `authRequestId`.
 */
export function parseDeepLinkUrl(deepLinkUrl: string): DeepLinkParams {
  // decentraland:// URLs use query params after `?`, same as HTTP URLs.
  // Replace the scheme so the URL constructor can parse it.
  const parseable = deepLinkUrl.replace(/^decentraland:\/\//, 'https://placeholder/')
  const url = new URL(parseable)
  return {
    signin: url.searchParams.get('signin'),
    authRequestId: url.searchParams.get('authRequestId')
  }
}

// ─── Auth-server identity fetching ──────────────────────────────────────────

export interface DeepLinkIdentity {
  expiration: string
  ephemeralIdentity: {
    address: string
    publicKey: string
  }
  authChain: Array<{
    type: string
    payload: string
    signature: string
  }>
}

/**
 * Fetches an identity by ID from the auth server's `/identities/{id}`
 * endpoint — the same call the Explorer's `DappDeepLinkAuthenticator` makes
 * after receiving the deep link.
 *
 * Returns the identity payload, or throws on HTTP errors.
 */
export async function fetchIdentity(identityId: string): Promise<DeepLinkIdentity> {
  const res = await fetch(`${authServerUrl()}/identities/${identityId}`, {
    signal: AbortSignal.timeout(15_000)
  })
  if (!res.ok) {
    throw new Error(`fetchIdentity(${identityId}) failed: ${res.status} ${await res.text()}`)
  }
  const json = (await res.json()) as { identity: DeepLinkIdentity }
  return json.identity
}
