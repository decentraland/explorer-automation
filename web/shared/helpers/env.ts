import { config as loadDotenv } from 'dotenv'
import { fileURLToPath } from 'url'
import { dirname, resolve } from 'path'

// `.env` lives at the repo root (three levels up from `web/shared/helpers/`).
// Loaded once on first import.
const here = dirname(fileURLToPath(import.meta.url))
loadDotenv({ path: resolve(here, '../../../.env') })

export function requireEnv(name: string): string {
  const value = process.env[name]
  if (value === undefined || value === '') {
    throw new Error(`Required environment variable ${name} is not set. Copy .env.example to .env and fill it in.`)
  }
  return value
}

export function optionalEnv(name: string): string | undefined {
  const value = process.env[name]
  return value === undefined || value === '' ? undefined : value
}

/**
 * Base URL of the Decentraland dapp under test (no trailing slash). Defaults
 * to production. Override with `WEB_BASE_URL` to target a different
 * environment (e.g. `https://decentraland.zone` or `https://decentraland.today`
 * for development / staging).
 */
export function getBaseUrl(): string {
  const raw = optionalEnv('WEB_BASE_URL') ?? 'https://decentraland.org'
  return raw.replace(/\/+$/, '')
}

/** Dapp hosts that sit behind Cloudflare Access and require the service-token headers. */
const CF_GATED_HOSTS = new Set(['decentraland.zone', 'decentraland.today'])

/** Whether `host` is one of the Cloudflare-Access-gated dapp hosts (`.zone` / `.today`). */
export function isCfGatedHost(host: string): boolean {
  return CF_GATED_HOSTS.has(host)
}

/**
 * Cloudflare Access service-token headers, if all of the following hold:
 *
 *  1. `CF_ACCESS_CLIENT_ID` and `CF_ACCESS_CLIENT_SECRET` are both set, AND
 *  2. the dapp host under test (`getBaseUrl()` → `WEB_BASE_URL`, default `.org`)
 *     is one of the CF-gated dev/staging dapps — `decentraland.zone` or
 *     `decentraland.today`.
 *
 * Returns `{}` otherwise — safe to spread into any `headers` object.
 *
 * The headers must ride ONLY on requests whose target host is CF-gated —
 * never on cross-origin requests. Attaching a non-safelisted header to a
 * CORS-mode request (module scripts / CSS from `cdn.decentraland.org`, fonts
 * from `fonts.gstatic.com`) forces a preflight those origins reject, which
 * blocks every dapp JS bundle and renders pages blank. That is why these
 * headers are injected via the host-scoped `context.route()` in
 * `shared/fixtures/base-test.ts` (`installCfAccessRoute`) and NOT via
 * Playwright's `use.extraHTTPHeaders`, which applies to every request. The
 * same mechanism is what previously broke `.org` runs when the headers were
 * sent unconditionally (`.org` pulls its bundles from the same CDN) — the
 * host gate here keeps them off `.org` runs entirely.
 */
export function getCloudflareAccessHeaders(): Record<string, string> {
  const id = optionalEnv('CF_ACCESS_CLIENT_ID')
  const secret = optionalEnv('CF_ACCESS_CLIENT_SECRET')
  if (!id || !secret) return {}

  const host = new URL(getBaseUrl()).host
  if (!CF_GATED_HOSTS.has(host)) return {}

  return {
    'CF-Access-Client-Id': id,
    'CF-Access-Client-Secret': secret
  }
}
