import path from 'node:path';
import { fileURLToPath } from 'node:url';
import dotenv from 'dotenv';

const __dirname = path.dirname(fileURLToPath(import.meta.url));

// `.env` lives at the repo root, one level above `web/`. Loaded once on first import.
dotenv.config({ path: path.resolve(__dirname, '../../.env') });

export function requireEnv(name: string): string {
  const value = process.env[name];
  if (!value || value.trim() === '') {
    throw new Error(
      `Environment variable ${name} is not set. Add it to your .env file at the repo root (see .env.example).`,
    );
  }
  return value;
}

export function optionalEnv(name: string): string | undefined {
  const value = process.env[name];
  return value && value.trim() !== '' ? value : undefined;
}

/**
 * Base URL of the Decentraland dapp under test (no trailing slash). Defaults
 * to production. Override with `WEB_BASE_URL` to target a different
 * environment (e.g. `https://decentraland.zone` for development).
 */
export function getBaseUrl(): string {
  const raw = optionalEnv('WEB_BASE_URL') ?? 'https://decentraland.org';
  return raw.replace(/\/+$/, '');
}
