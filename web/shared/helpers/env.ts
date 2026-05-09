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
 * environment (e.g. `https://decentraland.zone` for development).
 */
export function getBaseUrl(): string {
  const raw = optionalEnv('WEB_BASE_URL') ?? 'https://decentraland.org'
  return raw.replace(/\/+$/, '')
}
