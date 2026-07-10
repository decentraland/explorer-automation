/**
 * Redacts secrets from text that is about to be logged, attached to a test
 * report, or echoed to CI stdout. Used by the browser-log failure diagnostics
 * in `shared/fixtures/base-test.ts` — the log deliberately never records
 * request/response headers, but console messages and URLs can still carry
 * credentials (an RPC URL embeds its API key, a dapp error might echo a
 * token), so everything passes through here before it is stored.
 *
 * Two layers:
 *
 *  1. **Exact env-value redaction** — the value of every env var whose name
 *     looks secret-bearing (`*_SECRET`, `*_PASSWORD`, `*_PRIVATE_KEY`,
 *     `*_TOKEN`, `*_API_KEY`), plus explicitly-listed vars whose names don't
 *     match but whose values are credentials (`CF_ACCESS_CLIENT_ID` is half
 *     of the CF service token). Both the raw and the URL-encoded form are
 *     replaced with the var name, e.g. `<CF_ACCESS_CLIENT_SECRET>`, so the
 *     log stays readable.
 *
 *  2. **Pattern redaction** — credential shapes that need no env knowledge:
 *     JWTs, `Bearer` tokens, basic-auth userinfo in URLs, and the values of
 *     query params with credential-like names.
 *
 * Not covered on purpose:
 *
 *  - 0x-prefixed 32-byte hex strings — that shape is shared by tx hashes and
 *    block hashes, core diagnostic data. The only private keys that matter
 *    (the funded pool wallets) come from env and are caught by layer 1;
 *    per-test throwaway keys from `generatePrivateKey()` guard empty
 *    unfunded accounts, so a leak of one is harmless.
 *  - `POLYGON_AMOY_RPC_URL` / `SEPOLIA_RPC_URL` — the endpoints this repo
 *    uses are public, not keyed, so keeping them readable in `requestfailed`
 *    lines is worth more than redacting them. If a keyed provider URL is
 *    ever adopted, add the vars to EXTRA_SENSITIVE_ENV_VARS below.
 */

// Env vars with credential values whose names don't match SECRET_NAME_PATTERN.
const EXTRA_SENSITIVE_ENV_VARS = ['CF_ACCESS_CLIENT_ID']

const SECRET_NAME_PATTERN = /(_SECRET|_PASSWORD|_PRIVATE_KEY|_TOKEN|_API_KEY)$/

// Values shorter than this are too likely to collide with innocent text
// (ports, flags, "true") for a blind global replace.
const MIN_SECRET_LENGTH = 8

type Replacement = { needle: string; placeholder: string }

let cachedReplacements: Replacement[] | null = null

function envReplacements(): Replacement[] {
  if (cachedReplacements) return cachedReplacements
  const replacements: Replacement[] = []
  for (const [name, value] of Object.entries(process.env)) {
    if (!value || value.length < MIN_SECRET_LENGTH) continue
    if (!SECRET_NAME_PATTERN.test(name) && !EXTRA_SENSITIVE_ENV_VARS.includes(name)) continue
    replacements.push({ needle: value, placeholder: `<${name}>` })
    // Percent-encoding is case-insensitive (RFC 3986): encodeURIComponent
    // emits uppercase hex (%2B) but other encoders emit lowercase (%2b), so
    // cover both. Mixed-case escapes within one value aren't covered — no
    // real-world encoder produces them.
    const encoded = encodeURIComponent(value)
    if (encoded !== value) {
      replacements.push({ needle: encoded, placeholder: `<${name}>` })
      const lowercaseHex = encoded.replace(/%[0-9A-F]{2}/g, escape => escape.toLowerCase())
      if (lowercaseHex !== encoded) replacements.push({ needle: lowercaseHex, placeholder: `<${name}>` })
    }
  }
  // Longest first so a value that contains another secret as a substring is
  // replaced whole before the shorter needle can split it.
  cachedReplacements = replacements.sort((a, b) => b.needle.length - a.needle.length)
  return cachedReplacements
}

const PATTERN_REDACTIONS: Array<{ pattern: RegExp; replacement: string }> = [
  // JWT — three dot-separated base64url segments starting with an `eyJ` header.
  { pattern: /\beyJ[\w-]{8,}\.[\w-]{4,}\.[\w-]{4,}\b/g, replacement: '<JWT>' },
  // Authorization-style bearer tokens quoted in console text. The scheme is
  // case-insensitive (RFC 7235) — capture it so the original casing survives.
  { pattern: /\b(Bearer)\s+[\w.~+/-]{8,}=*/gi, replacement: '$1 <TOKEN>' },
  // Basic-auth userinfo in URLs — https://user:pass@host.
  { pattern: /(\/\/[^\s/:@]+:)[^\s@/]+@/g, replacement: '$1<REDACTED>@' },
  // Values of credential-named query params. `key` (GCP/Firebase-style) and
  // `code` (OAuth authorization codes) are included even though they can
  // occasionally name innocent data — over-redacting a failure log beats
  // leaking through it.
  {
    pattern:
      /([?&](?:access_token|id_token|refresh_token|token|apikey|api_key|key|client_secret|secret|password|private_key|signature|auth|otp|code)=)[^&\s"']+/gi,
    replacement: '$1<REDACTED>'
  }
]

/** Returns `text` with every known secret value and credential-shaped pattern redacted. */
export function sanitizeForLog(text: string): string {
  let result = text
  for (const { needle, placeholder } of envReplacements()) {
    result = result.replaceAll(needle, placeholder)
  }
  for (const { pattern, replacement } of PATTERN_REDACTIONS) {
    result = result.replace(pattern, replacement)
  }
  return result
}
