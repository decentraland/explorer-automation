import { randomBytes } from 'node:crypto'
import { ImapFlow, type FetchMessageObject } from 'imapflow'
import { simpleParser } from 'mailparser'
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
  const domain = optionalEnv('EMAIL_DOMAIN') ?? 'e2e.decentraland.org'
  const local = `qa-${randomBytes(4).toString('hex')}`
  return `${local}@${domain}`
}

export interface WaitForOtpOptions {
  timeoutMs?: number
  pollIntervalMs?: number
}

/**
 * Connect to IMAP and poll for an OTP email addressed to `toAddress` from the
 * configured Thirdweb sender. Returns the 6-digit code from the message body.
 *
 * Captures `startedAt` at entry and only accepts messages whose `internalDate`
 * is at or after that moment. This guards against stale OTPs left in the
 * `IMAP_USER` mailbox from prior runs (or from an earlier phase of the same
 * test) — the IMAP `(to, from)` search is unscoped by date, so the newest
 * matching UID could otherwise be from minutes/hours ago, and the dapp would
 * reject the stale code on submit.
 */
export async function waitForOtp(toAddress: string, options: WaitForOtpOptions = {}): Promise<string> {
  const startedAt = new Date()
  const timeoutMs = options.timeoutMs ?? 90_000
  const pollIntervalMs = options.pollIntervalMs ?? 3_000

  const host = requireEnv('IMAP_HOST')
  const port = Number.parseInt(requireEnv('IMAP_PORT'), 10)
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
        const searchResult = await client.search({ to: toAddress, from: fromAddress }, { uid: true })
        const uids = searchResult || []
        // Walk newest-to-oldest. UIDs are server-monotonic so ordering reflects
        // arrival; bail at the first message whose internalDate is before
        // `startedAt` since every UID below it must also be stale.
        for (let i = uids.length - 1; i >= 0; i--) {
          const uid = uids[i]!
          const message = await client.fetchOne(String(uid), { source: true, internalDate: true }, { uid: true })
          if (!message) continue
          if (message.internalDate && message.internalDate < startedAt) {
            // eslint-disable-next-line no-console
            console.log('[otp] reached stale UID — fresh OTP not yet delivered')
            break
          }
          const code = await extractCode(message)
          if (code) {
            // eslint-disable-next-line no-console
            console.log('[otp] code extracted')
            return code
          }
          // eslint-disable-next-line no-console
          console.log('[otp] message arrived but no 6-digit code in body — checking older')
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

async function extractCode(message: FetchMessageObject): Promise<string | undefined> {
  if (!message.source) return undefined
  // Parse the MIME message so we search only decoded body text — NOT headers.
  // Headers commonly contain 6-digit runs (Message-IDs, DKIM signatures, timestamps)
  // that would otherwise produce false positives.
  const parsed = await simpleParser(message.source)
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
