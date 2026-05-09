import { randomBytes } from 'node:crypto'
import { ImapFlow } from 'imapflow'
import { simpleParser, type AddressObject, type ParsedMail } from 'mailparser'
import { requireEnv, optionalEnv } from '../../../shared/helpers/env.js'

const SIX_DIGIT_CODE = /\d{6}/

/**
 * Generate a fresh test email of the form `qa-<hash>@<EMAIL_DOMAIN>`. Each
 * call returns a distinct local-part so every signup looks like a brand-new
 * recipient to Thirdweb (its own per-address rate-limit bucket — no curated
 * fallback list needed).
 *
 * Defaults to `e2e.decentraland.org`, a Workspace catch-all whose deliveries
 * route to the inbox at `IMAP_USER`. Override with `EMAIL_DOMAIN` if you've
 * pointed the suite at a different inbox or domain.
 */
export function generateFreshEmail(): string {
  // Trim + strip a stray leading '@' so a slightly-malformed EMAIL_DOMAIN
  // (e.g. "@e2e.decentraland.org" or " e2e.decentraland.org ") still produces
  // a valid recipient.
  const raw = optionalEnv('EMAIL_DOMAIN') ?? 'e2e.decentraland.org'
  const domain = raw.trim().replace(/^@/, '')
  const local = `qa-${randomBytes(4).toString('hex')}`
  return `${local}@${domain}`
}

export interface WaitForOtpOptions {
  timeoutMs?: number
  pollIntervalMs?: number
}

/**
 * Connect to IMAP and poll for an OTP email **explicitly addressed to**
 * `toAddress` from the configured Thirdweb sender. Returns the 6-digit code
 * from the message body.
 *
 * Recipient verification — why we don't just trust the IMAP `to:` filter:
 *   The dapp catch-all forwarder (Cloudflare Email Routing for
 *   `*@e2e.decentraland.org`) doesn't reliably preserve the original `To:`
 *   header when delivering forwarded mail to `IMAP_USER`'s mailbox; some
 *   forwarders rewrite To to the destination, others tuck the original into
 *   `Delivered-To` / `X-Original-To`, and a few only echo the recipient in
 *   the body. So the IMAP-level `to:` filter would silently miss our
 *   legitimate emails. Instead we filter the IMAP search by sender only and
 *   then **verify on every candidate** that `toAddress` actually appears in
 *   one of: To / Cc / Bcc / Delivered-To / X-Original-To, with the message
 *   body as a final fallback. This is robust under any forwarder behaviour
 *   and also rules out picking up a concurrent test run's OTP that landed
 *   in the same shared mailbox.
 *
 * Stale-OTP guard:
 *   Captures `startedAt` (with a 5s buffer to absorb the gap between
 *   `auth.submitEmail()` triggering the send and this function being
 *   called) and rejects any message whose `internalDate` is before that
 *   moment. Walks UIDs newest-first and bails at the first stale message
 *   since every UID below it must also be stale. The 5s buffer is well
 *   under the typical phase-1 → phase-2 transition gap (~15s+) on the
 *   recurrent OTP test, so it never lets a prior-phase OTP slip through.
 */
export async function waitForOtp(toAddress: string, options: WaitForOtpOptions = {}): Promise<string> {
  const startedAt = new Date(Date.now() - 5_000)
  const timeoutMs = options.timeoutMs ?? 90_000
  const pollIntervalMs = options.pollIntervalMs ?? 3_000

  const host = requireEnv('IMAP_HOST')
  const portStr = requireEnv('IMAP_PORT')
  const port = Number.parseInt(portStr, 10)
  if (!Number.isInteger(port) || port < 1 || port > 65535) {
    throw new Error(`IMAP_PORT must be a valid port number (1-65535), got "${portStr}"`)
  }
  const user = requireEnv('IMAP_USER')
  const password = requireEnv('IMAP_PASSWORD')
  const fromAddress = requireEnv('OTP_FROM_EMAIL')

  // eslint-disable-next-line no-console
  console.log(`[otp] waiting for email to ${toAddress} from ${fromAddress} (timeout ${timeoutMs / 1000}s)`)

  const client = new ImapFlow({
    host,
    port,
    secure: true,
    auth: { user, pass: password },
    logger: false
  })

  await client.connect()
  try {
    const lock = await client.getMailboxLock('INBOX')
    try {
      const deadline = Date.now() + timeoutMs
      while (Date.now() < deadline) {
        // Filter by sender only at the IMAP level — recipient is verified
        // post-fetch (see emailIsForRecipient) because forwarders rewrite To.
        const searchResult = await client.search({ from: fromAddress }, { uid: true })
        const uids = searchResult || []
        for (let i = uids.length - 1; i >= 0; i--) {
          const uid = uids[i]!
          const message = await client.fetchOne(String(uid), { source: true, internalDate: true }, { uid: true })
          if (!message) continue
          if (message.internalDate && message.internalDate < startedAt) {
            // eslint-disable-next-line no-console
            console.log('[otp] reached stale UID — fresh OTP not yet delivered')
            break
          }
          if (!message.source) continue
          const parsed = await simpleParser(message.source)
          if (!emailIsForRecipient(parsed, toAddress)) {
            // From Thirdweb but not addressed to us — concurrent run's OTP,
            // or a noise email. Skip and check older.
            continue
          }
          const code = extractCodeFromParsed(parsed)
          if (code) {
            // eslint-disable-next-line no-console
            console.log(`[otp] code extracted (recipient verified for ${toAddress})`)
            return code
          }
          // eslint-disable-next-line no-console
          console.log('[otp] recipient matched but no 6-digit code in body — checking older')
        }
        await sleep(pollIntervalMs)
      }
      throw new Error(`OTP email to ${toAddress} from ${fromAddress} not received within ${timeoutMs / 1000}s`)
    } finally {
      lock.release()
    }
  } finally {
    await client.logout().catch(() => undefined)
  }
}

/**
 * True iff `expectedTo` appears in any recipient-bearing header
 * (To / Cc / Bcc / Delivered-To / X-Original-To) or — as a final fallback —
 * in the message body. Case-insensitive substring match: covers headers
 * shaped as `"Name" <addr@host>` and forwarders that wrap multiple
 * addresses on one line.
 */
function emailIsForRecipient(parsed: ParsedMail, expectedTo: string): boolean {
  const expected = expectedTo.trim().toLowerCase()
  if (!expected) return false

  const candidates = [
    addressText(parsed.to),
    addressText(parsed.cc),
    addressText(parsed.bcc),
    headerText(parsed.headers.get('delivered-to')),
    headerText(parsed.headers.get('x-original-to'))
  ].filter((v): v is string => typeof v === 'string' && v.length > 0)

  for (const c of candidates) {
    if (c.toLowerCase().includes(expected)) return true
  }

  // Body fallback: Thirdweb-style OTP emails sometimes echo the recipient.
  const body = `${parsed.text ?? ''}\n${stripHtml(parsed.html || '')}`
  return body.toLowerCase().includes(expected)
}

function addressText(addr: AddressObject | AddressObject[] | undefined): string | undefined {
  if (!addr) return undefined
  if (Array.isArray(addr)) return addr.map(a => a.text ?? '').join(' ')
  return addr.text
}

function headerText(value: unknown): string | undefined {
  if (typeof value === 'string') return value
  if (Array.isArray(value)) {
    const strings = value.filter((v): v is string => typeof v === 'string')
    return strings.length > 0 ? strings.join(' ') : undefined
  }
  return undefined
}

function extractCodeFromParsed(parsed: ParsedMail): string | undefined {
  // Parse the MIME message so we search only decoded body text — NOT headers.
  // Headers commonly contain 6-digit runs (Message-IDs, DKIM signatures,
  // timestamps) that would otherwise produce false positives.
  const candidates = [parsed.text ?? '', stripHtml(parsed.html || '')]
  for (const body of candidates) {
    const match = SIX_DIGIT_CODE.exec(body)
    if (match) return match[0]
  }
  return undefined
}

function stripHtml(html: string | false | undefined): string {
  if (!html) return ''
  return html.replace(/<[^>]+>/g, ' ')
}

function sleep(ms: number): Promise<void> {
  return new Promise(resolve => setTimeout(resolve, ms))
}
