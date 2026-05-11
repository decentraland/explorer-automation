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

/**
 * Cloudflare Access service-token headers, if all of the following hold:
 *
 *  1. `CF_ACCESS_CLIENT_ID` and `CF_ACCESS_CLIENT_SECRET` are both set, AND
 *  2. the dapp host under test (resolved from `WEB_BASE_URL`, falling back to
 *     `BASE_URL`, then `.org`) is one of the CF-gated dev/staging dapps —
 *     `decentraland.zone` or `decentraland.today`.
 *
 * Returns `{}` otherwise — safe to spread into any `headers` object.
 *
 * **Why the host gate exists.** Earlier wiring spread the headers onto every
 * request regardless of target, on the assumption that non-gated hosts would
 * ignore them. That assumption proved wrong against `decentraland.org`:
 * Cloudflare sitting in front of the prod origin reacts to the headers
 * (anti-bot heuristics or Access-flow side effects) and returns subtly
 * different responses, breaking landing-page and auth flows that work fine
 * without the headers. So we narrow the emission to hosts where the token
 * is actually required.
 */
export function getCloudflareAccessHeaders(): Record<string, string> {
  const id = optionalEnv('CF_ACCESS_CLIENT_ID')
  const secret = optionalEnv('CF_ACCESS_CLIENT_SECRET')
  if (!id || !secret) return {}

  const baseUrl = optionalEnv('WEB_BASE_URL') ?? optionalEnv('BASE_URL') ?? 'https://decentraland.org'
  const host = new URL(baseUrl).host
  if (!CF_GATED_HOSTS.has(host)) return {}

  return {
    'CF-Access-Client-Id': id,
    'CF-Access-Client-Secret': secret
  }
}
