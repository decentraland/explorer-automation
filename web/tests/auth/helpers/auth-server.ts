import { optionalEnv } from '../../../shared/helpers/env.js'
import type { AuthChain } from '../../../shared/helpers/identity.js'

/**
 * HTTP client for the Decentraland auth server (`auth-api.decentraland.<env>`).
 *
 * The auth server brokers signature requests between a desktop client (or any
 * out-of-band signer) and a wallet that lives in a browser session. The desktop
 * side POSTs a request describing what it wants signed and gets back a
 * `requestId`; it then surfaces a URL `<WEB_BASE_URL>/auth/requests/<id>` to
 * the user, who completes the signing in their browser. The desktop polls
 * the auth server for the outcome.
 *
 * Host resolution is in `authServerUrl()` below — derives from `WEB_BASE_URL`
 * so a run against `decentraland.today` automatically talks to
 * `auth-api.decentraland.today`.
 *
 * Used by the RequestPage tests (`auth-request-page.spec.ts`) to simulate the
 * desktop side of that handshake.
 */

const DEFAULT_POLL_TIMEOUT_MS = 15_000
const POLL_INTERVAL_MS = 1_000

export interface CreateRequestResult {
  requestId: string
  expiration: string
  code: number
}

export interface RequestOutcome {
  sender: string
  requestId: string
  result?: string
  error?: { code: number; message: string }
}

/**
 * Resolves the auth-api host for the environment under test. Resolution rule:
 *
 *  1. `AUTH_SERVER_URL` env var — explicit override, used verbatim (trailing
 *     slash stripped). Set this when you want the spec to talk to a different
 *     auth-api than the one paired with `BASE_URL`'s host (e.g. running
 *     against the .org dapp but using zone's auth-api so the dapp picks up
 *     testnet contracts — see `dappEnvQuery()` below).
 *  2. Otherwise derive `auth-api.<host>` from `WEB_BASE_URL` (or `BASE_URL`
 *     as a fallback). So `WEB_BASE_URL=https://decentraland.today` →
 *     `https://auth-api.decentraland.today`.
 *  3. Final fallback: `https://auth-api.decentraland.org`.
 *
 * Reads env vars at call time so tests that override env vars in `beforeAll`
 * see the new value.
 */
export function authServerUrl(): string {
  const explicit = optionalEnv('AUTH_SERVER_URL')
  if (explicit) return explicit.replace(/\/+$/, '')

  const baseUrl = optionalEnv('WEB_BASE_URL') ?? optionalEnv('BASE_URL') ?? 'https://decentraland.org'
  const host = new URL(baseUrl).host
  return `https://auth-api.${host}`
}

/**
 * The `?env=…` query value the dapp expects so it boots into the testnet /
 * staging contract list matching the spec's auth-api. Derived from the
 * resolved `authServerUrl()` host: a spec talking to `auth-api.decentraland.zone`
 * must drive the dapp with `?env=dev` so the dapp talks to the same zone
 * services (transactions-api, marketplace-api, etc.). `''` means no query
 * string is appended.
 *
 * The user knob is therefore just `AUTH_SERVER_URL` (or `BASE_URL`); this
 * function derives the matching dapp env so the two stay consistent.
 */
export function dappEnvQuery(): string {
  const host = new URL(authServerUrl()).host
  if (host.endsWith('decentraland.zone')) return 'dev'
  if (host.endsWith('decentraland.today')) return 'today'
  return ''
}

/**
 * Sibling-service URL on the same host as `authServerUrl()` — e.g.
 * `authPairedServiceUrl('transactions-api')` returns the transactions-api
 * origin for whatever env the auth-api is on. Keeps the two in lockstep
 * without each call site re-doing the host swap.
 */
export function authPairedServiceUrl(subdomain: string): string {
  const url = new URL(authServerUrl())
  url.host = url.host.replace(/^auth-api\./, `${subdomain}.`)
  return url.origin
}

/**
 * Creates a new auth request on the server.
 *
 * @param method     The RPC method (e.g. `dcl_personal_sign`, `eth_sendTransaction`).
 * @param params     The method parameters.
 * @param authChain  Required for non-`dcl_personal_sign` methods.
 */
export async function createAuthRequest(
  method: string,
  params: unknown[],
  authChain?: AuthChain
): Promise<CreateRequestResult> {
  const body: Record<string, unknown> = { method, params }
  if (authChain) body.authChain = authChain

  const res = await fetch(`${authServerUrl()}/requests`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body)
  })
  if (!res.ok) {
    throw new Error(`createAuthRequest failed: ${res.status} ${await res.text()}`)
  }
  return (await res.json()) as CreateRequestResult
}

/**
 * Polls the auth server for the outcome of a request. The server returns 204
 * while the request is still pending; once a wallet signs (or rejects), it
 * responds with the outcome JSON.
 */
export async function pollAuthOutcome(requestId: string, timeoutMs = DEFAULT_POLL_TIMEOUT_MS): Promise<RequestOutcome> {
  const deadline = Date.now() + timeoutMs
  while (Date.now() < deadline) {
    const res = await fetch(`${authServerUrl()}/requests/${requestId}`)
    if (res.status === 204) {
      await new Promise(r => setTimeout(r, POLL_INTERVAL_MS))
      continue
    }
    if (!res.ok) {
      throw new Error(`pollAuthOutcome failed: ${res.status} ${await res.text()}`)
    }
    return (await res.json()) as RequestOutcome
  }
  throw new Error(`Polling for request ${requestId} timed out after ${timeoutMs}ms`)
}

/**
 * Extracts the tx hash from an `eth_sendTransaction` outcome, throwing if
 * the wallet returned an error rather than a signature. Helper exists so
 * on-chain specs don't need a local `if (!result) throw` (which Playwright's
 * `no-conditional-in-test` rule flags) and so the error formatting stays
 * consistent across specs.
 */
export function requireTxHash(outcome: RequestOutcome): `0x${string}` {
  const hash = outcome.result
  if (!hash) throw new Error(`expected tx hash from auth outcome (got error: ${JSON.stringify(outcome.error)})`)
  return hash as `0x${string}`
}
