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

/**
 * Cloudflare Access service-token headers, if both `CF_ACCESS_CLIENT_ID`
 * and `CF_ACCESS_CLIENT_SECRET` are set. Required for browser navigation to
 * the dev/staging dapp hosts — `WEB_BASE_URL=https://decentraland.zone` and
 * `WEB_BASE_URL=https://decentraland.today` — which are the only dapp origins
 * gated behind CF Access. The `*.api.decentraland.zone` / `.today` subdomains
 * (auth-api, marketplace-api) are publicly reachable.
 *
 * Returns `{}` when either env var is missing — safe to spread into any
 * `headers` object. Non-gated hosts ignore these headers, so the broad
 * `extraHTTPHeaders` wiring in `playwright.config.ts` is harmless even
 * though only the dapp navigation strictly needs the tokens.
 */
export function getCloudflareAccessHeaders(): Record<string, string> {
  const id = optionalEnv('CF_ACCESS_CLIENT_ID')
  const secret = optionalEnv('CF_ACCESS_CLIENT_SECRET')
  if (!id || !secret) return {}
  return {
    'CF-Access-Client-Id': id,
    'CF-Access-Client-Secret': secret
  }
}
