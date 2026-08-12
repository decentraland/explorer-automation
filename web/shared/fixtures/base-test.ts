import { test as base, type BrowserContext, type TestInfo } from '@playwright/test'
import { getCloudflareAccessHeaders, isCfGatedHost } from '../helpers/env.js'
import { sanitizeForLog } from '../helpers/log-sanitizer.js'

/**
 * Installs a context-level route that attaches the Cloudflare Access
 * service-token headers to requests targeting the CF-gated dapp hosts
 * (`decentraland.zone` / `decentraland.today`) — and only to those.
 *
 * Deliberately NOT `use.extraHTTPHeaders`: Playwright applies those to every
 * request the browser makes, including cross-origin CORS-mode requests
 * (module scripts / CSS from `cdn.decentraland.org`, fonts from
 * `fonts.gstatic.com`). A non-safelisted header on a CORS request forces a
 * preflight those origins reject, so every dapp JS bundle fails to load and
 * pages render blank. The breakage is masked while any `page.route()` is
 * registered — Playwright's interception path skips the preflight — which is
 * what made the July 2026 `.zone` failures look spec- and phase-dependent
 * (web3 signup passed while wallet-mock routes were active; the same page
 * went blank the moment a helper unrouted).
 *
 * Context-level (not page-level) so helper teardowns that call
 * `page.unroute(...)` can't remove it, and popups inherit it. No-op when
 * `getCloudflareAccessHeaders()` returns `{}` (env vars missing or a
 * non-gated `WEB_BASE_URL`), so `.org` runs are untouched.
 */
export async function installCfAccessRoute(context: BrowserContext): Promise<void> {
  const headers = getCloudflareAccessHeaders()
  if (Object.keys(headers).length === 0) return
  await context.route(
    url => isCfGatedHost(url.host),
    route => route.continue({ headers: { ...route.request().headers(), ...headers } })
  )
}

// Caps keep a chatty 240s test from hoarding memory or drowning the CI log.
// When the cap is hit the render notes how many entries were dropped, so a
// truncated log never silently reads as "nothing else happened".
const MAX_LOG_ENTRIES = 1_000
const MAX_ENTRY_LENGTH = 600

/**
 * Collects a text log of everything diagnostically useful the browser does:
 * console errors/warnings, uncaught page errors, failed requests (with the
 * network-layer error), HTTP >= 400 responses, and main-frame navigations.
 * Timestamps are seconds since collection started.
 *
 * This is the replacement for `trace`/`video` recording (removed from
 * playwright.config.ts — 1GB+ artifacts, and traces embedded the CF Access
 * service token in recorded request headers). Only URLs, statuses, and
 * console text are logged — never request or response headers — and every
 * entry passes through `sanitizeForLog` (shared/helpers/log-sanitizer.ts),
 * which redacts known env secret values and credential-shaped patterns, so
 * gated-host runs can't leak the token through this log.
 */
function installBrowserLogCollector(context: BrowserContext): { render(): string } {
  const started = Date.now()
  const entries: string[] = []
  let dropped = 0

  const push = (kind: string, detail: string): void => {
    if (entries.length >= MAX_LOG_ENTRIES) {
      dropped += 1
      return
    }
    const seconds = ((Date.now() - started) / 1000).toFixed(1).padStart(7)
    // Sanitize BEFORE truncating — a secret cut in half by the length cap
    // would no longer exact-match its redaction needle and could leak.
    const line = sanitizeForLog(`${seconds}s  ${kind.padEnd(14)} ${detail}`)
    entries.push(line.length > MAX_ENTRY_LENGTH ? `${line.slice(0, MAX_ENTRY_LENGTH)}…` : line)
  }

  context.on('page', page => {
    page.on('console', msg => {
      const type = msg.type()
      // error/warning only — dapp info logs (analytics, previews) are pure
      // volume; the caps would fill with noise before the useful entries.
      if (type !== 'error' && type !== 'warning') return
      const { url, lineNumber } = msg.location()
      push(`console.${type}`, `${msg.text()} [${url}:${lineNumber}]`)
    })
    page.on('pageerror', error => push('pageerror', error.message))
    page.on('requestfailed', request => {
      push('requestfailed', `${request.method()} ${request.url()} — ${request.failure()?.errorText ?? 'unknown error'}`)
    })
    page.on('response', response => {
      if (response.status() >= 400) {
        push(`http ${response.status()}`, `${response.request().method()} ${response.url()}`)
      }
    })
    page.on('framenavigated', frame => {
      if (frame === page.mainFrame()) push('navigated', frame.url())
    })
    page.on('close', () => push('page closed', page.url()))
  })

  return {
    render(): string {
      const openPages = context
        .pages()
        .map(p => `  ${sanitizeForLog(p.url())}`)
        .join('\n')
      return [
        entries.length === 0
          ? '(no console errors/warnings, failed requests, or >=400 responses recorded)'
          : entries.join('\n'),
        dropped > 0 ? `… ${dropped} further entries dropped after the ${MAX_LOG_ENTRIES}-entry cap` : null,
        openPages ? `open pages at teardown:\n${openPages}` : 'no pages open at teardown'
      ]
        .filter((part): part is string => part !== null)
        .join('\n')
    }
  }
}

/**
 * Shared `context` fixture override: CF Access route + browser-log capture.
 * On failure (or timeout — any status differing from the expected one) the
 * log is attached to the report as `browser-log` AND echoed to stdout so it
 * lands verbatim in the CI job log. Both `test` below and the wallet
 * fixtures layer this in — new fixture files should do the same.
 */
export async function contextWithDiagnostics(
  { context }: { context: BrowserContext },
  use: (context: BrowserContext) => Promise<void>,
  testInfo: TestInfo
): Promise<void> {
  await installCfAccessRoute(context)
  const browserLog = installBrowserLogCollector(context)
  await use(context)
  if (testInfo.status !== testInfo.expectedStatus) {
    const body = browserLog.render()
    await testInfo.attach('browser-log', { body, contentType: 'text/plain' })
    console.log(`\n[browser-log] ${testInfo.titlePath.join(' › ')}\n${body}\n`)
  }
}

/**
 * Drop-in replacement for `@playwright/test`'s `test` for specs that don't
 * need the Synpress wallet mock (OTP flows, landing). Adds the CF Access
 * route (required on `.zone` / `.today`, no-op on `.org`) and the verbose
 * browser log on failure. Wallet specs get the same behavior through
 * `shared/fixtures/wallet-fixture.ts`.
 *
 * Specs must import `test` from here (or from a wallet fixture) — never from
 * `@playwright/test` directly, or `.zone` / `.today` runs silently lose the
 * CF headers and time out on blank pages. ESLint enforces this.
 */
export const test = base.extend({
  context: contextWithDiagnostics
})

export { expect } from '@playwright/test'
