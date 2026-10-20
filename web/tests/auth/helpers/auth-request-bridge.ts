import fs from 'node:fs/promises'
import { sleep } from '../../../shared/helpers/async.js'
import { getCrossStackPath } from './persistent-data-path.js'

/**
 * The cross-stack handoff signal for the client-first wallet flow.
 *
 * The desktop Explorer writes `auth-url.txt` into its Unity
 * `Application.persistentDataPath` the moment it would call
 * `Application.OpenURL` for the wallet auth flow (compile-time-gated under
 * `#if ALTTESTER` in `UnityAppWebBrowser.OpenUrl` — see the matching
 * unity-explorer change). The Playwright spec polls for that file: its
 * appearance is the "Unity has fired the auth request" signal, and its
 * contents are the URL Playwright needs to navigate to.
 *
 * The URL shape (observed on `--dclenv org`):
 *   https://decentraland.org/auth/login?redirectTo=%2Fauth%2Frequests%2F<requestId>%3FtargetConfigId%3Ddefault
 *
 * From it we derive:
 *   - `url`: the full URL (what to navigate to)
 *   - `requestId`: extracted from the `redirectTo`-encoded path
 */
export interface AuthHandoffCapture {
  url: string
  requestId: string
}

/**
 * Path the unity-explorer ALTTESTER hook writes the opened URL to: Unity's
 * `Application.persistentDataPath` (`Decentraland/Explorer/` — see
 * `persistent-data-path.ts` for the runtime-verified resolution and why it is
 * not `Decentraland Unity Explorer`).
 */
export function getAuthUrlPath(): string {
  return getCrossStackPath('explorer', 'auth-url.txt')
}

export async function authUrlExists(): Promise<boolean> {
  try {
    await fs.access(getAuthUrlPath())
    return true
  } catch {
    return false
  }
}

/**
 * Parses the URL written by Unity into the structured handoff payload.
 *
 * The Unity client opens one of two URL shapes:
 *   - **Direct** (observed in practice when the client side fires the auth
 *     request): `https://<host>/auth/requests/<requestId>?loginMethod=METAMASK`.
 *     Source: `DappWeb3Authenticator.RequestSignatureAsync`.
 *   - **Login-wrapped** (rendered by the dapp when a user navigates to a
 *     requests page without being signed in): `https://<host>/auth/login
 *     ?redirectTo=%2Fauth%2Frequests%2F<requestId>%3FtargetConfigId%3Ddefault`.
 *     Source: dapp routing fallback.
 *
 * We extract the requestId from either form: first try the direct
 * `/auth/requests/<id>` in the pathname, then fall back to decoding the
 * `redirectTo` query param.
 */
export function parseAuthHandoffUrl(rawUrl: string): AuthHandoffCapture {
  const url = new URL(rawUrl)

  // Direct form: /auth/requests/<id> in the pathname.
  const directMatch = url.pathname.match(/\/auth\/requests\/([A-Za-z0-9_-]+)/)
  if (directMatch) {
    return { url: rawUrl, requestId: directMatch[1]! }
  }

  // Login-wrapped form: requestId hidden in URL-encoded redirectTo param.
  const redirectTo = url.searchParams.get('redirectTo')
  if (redirectTo) {
    const decoded = decodeURIComponent(redirectTo)
    const wrapped = decoded.match(/\/auth\/requests\/([A-Za-z0-9_-]+)/)
    if (wrapped) {
      return { url: rawUrl, requestId: wrapped[1]! }
    }
  }

  throw new Error(`auth-url.txt URL does not contain a parseable requestId: ${rawUrl}`)
}

/**
 * Polls for `auth-url.txt`, then returns the parsed handoff payload. Throws
 * if the file doesn't appear within `timeoutMs`.
 *
 * Default 60s timeout: covers Unity click → auth-api websocket connect →
 * auth-api response → `Application.OpenURL` chain.
 */
export async function waitForAuthHandoff(timeoutMs = 60_000, pollIntervalMs = 500): Promise<AuthHandoffCapture> {
  const deadline = Date.now() + timeoutMs
  while (Date.now() < deadline) {
    if (await authUrlExists()) {
      const raw = (await fs.readFile(getAuthUrlPath(), 'utf8')).trim()
      return parseAuthHandoffUrl(raw)
    }
    await sleep(pollIntervalMs)
  }
  throw new Error(`auth-url.txt did not appear at ${getAuthUrlPath()} within ${timeoutMs / 1000}s`)
}

/**
 * Removes auth-url.txt if present. Run at the start of an `@cross` Flow 1
 * spec so we observe a fresh write, not stale state from a prior run.
 */
export async function removeAuthHandoff(): Promise<void> {
  try {
    await fs.unlink(getAuthUrlPath())
  } catch (err) {
    if ((err as NodeJS.ErrnoException).code !== 'ENOENT') throw err
  }
}

/**
 * Path the C# `TestReadVerificationCode` stage writes Unity's
 * Verification.Dapp.Screen code value to. Mirrors `CrossPlatformPaths.AuthVerificationCodePath`
 * in the C# side — same Unity `persistentDataPath` dir as `getAuthUrlPath`.
 */
export function getAuthVerificationCodePath(): string {
  return getCrossStackPath('explorer', 'auth-verification-code.txt')
}

/**
 * Reads Unity's verification code written by the C# `TestReadVerificationCode`
 * stage. Throws if the file isn't present (caller is responsible for
 * orchestration order).
 */
export async function readUnityVerificationCode(): Promise<string> {
  return (await fs.readFile(getAuthVerificationCodePath(), 'utf8')).trim()
}

/** Remove a stale Unity verification-code file. */
export async function removeUnityVerificationCode(): Promise<void> {
  try {
    await fs.unlink(getAuthVerificationCodePath())
  } catch (err) {
    if ((err as NodeJS.ErrnoException).code !== 'ENOENT') throw err
  }
}
