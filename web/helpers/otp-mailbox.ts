import { randomBytes } from 'node:crypto';
import { ImapFlow, type FetchMessageObject } from 'imapflow';
import { simpleParser } from 'mailparser';
import { requireEnv, optionalEnv } from './env.js';

const SIX_DIGIT_CODE = /\d{6}/;

/**
 * Generate a fresh plus-alias email of the form `local+hash@domain`.
 * `baseAddress` defaults to `IMAP_USER`. The address must NOT already
 * contain a '+' alias — the hash is inserted before the '@'.
 */
export function generatePlusAliasEmail(baseAddress?: string): string {
  const base = baseAddress && baseAddress.length > 0 ? baseAddress : requireEnv('IMAP_USER');
  const atIdx = base.indexOf('@');
  if (atIdx <= 0) {
    throw new Error(`'${base}' is not a valid email address`);
  }
  const suffix = randomBytes(4).toString('hex');
  return `${base.slice(0, atIdx)}+${suffix}${base.slice(atIdx)}`;
}

export function getBaseEmail(): string {
  return requireEnv('IMAP_USER');
}

/**
 * Alternate signup base addresses for fallback when the primary hits Thirdweb's
 * per-address rate limit. All listed addresses MUST route to the inbox at
 * IMAP_USER (Gmail plus-aliases or domain aliases that forward to it),
 * since the OTP is read back via a single IMAP connection.
 */
export function getAlternateEmails(): string[] {
  const raw = optionalEnv('ALTERNATE_EMAILS');
  if (!raw) return [];
  return raw
    .split(',')
    .map((s) => s.trim())
    .filter((s) => s.length > 0);
}

export interface WaitForOtpOptions {
  timeoutMs?: number;
  pollIntervalMs?: number;
}

/**
 * Connect to IMAP and poll for an OTP email addressed to `toAddress` from the
 * configured Thirdweb sender. Returns the 6-digit code from the message body.
 *
 * Mirrors the C# OtpMailbox.WaitForOtp logic — deliberately does NOT filter by
 * SINCE/DeliveredAfter (day-only granularity bites us across timezone boundaries).
 * The unique TO alias prevents stale matches.
 */
export async function waitForOtp(
  toAddress: string,
  options: WaitForOtpOptions = {},
): Promise<string> {
  const timeoutMs = options.timeoutMs ?? 90_000;
  const pollIntervalMs = options.pollIntervalMs ?? 3_000;

  const host = requireEnv('IMAP_HOST');
  const port = Number.parseInt(requireEnv('IMAP_PORT'), 10);
  const user = requireEnv('IMAP_USER');
  const password = requireEnv('IMAP_PASSWORD');
  const fromAddress = requireEnv('IMAP_FROM_USER');

  // eslint-disable-next-line no-console
  console.log(
    `[otp] waiting for email to ${toAddress} from ${fromAddress} (timeout ${timeoutMs / 1000}s)`,
  );

  const client = new ImapFlow({
    host,
    port,
    secure: true,
    auth: { user, pass: password },
    logger: false,
  });

  await client.connect();
  try {
    const lock = await client.getMailboxLock('INBOX');
    try {
      const deadline = Date.now() + timeoutMs;
      while (Date.now() < deadline) {
        const searchResult = await client.search(
          { to: toAddress, from: fromAddress },
          { uid: true },
        );
        const uids = searchResult || [];
        if (uids.length > 0) {
          const newest = uids[uids.length - 1]!;
          const message = await client.fetchOne(
            String(newest),
            { source: true },
            { uid: true },
          );
          if (message) {
            const code = await extractCode(message);
            if (code) {
              // eslint-disable-next-line no-console
              console.log('[otp] code extracted');
              return code;
            }
            // eslint-disable-next-line no-console
            console.log('[otp] email arrived but no 6-digit code in body — retrying');
          }
        }
        await sleep(pollIntervalMs);
      }
      throw new Error(
        `OTP email to ${toAddress} from ${fromAddress} not received within ${timeoutMs / 1000}s`,
      );
    } finally {
      lock.release();
    }
  } finally {
    await client.logout().catch(() => undefined);
  }
}

async function extractCode(message: FetchMessageObject): Promise<string | undefined> {
  if (!message.source) return undefined;
  // Parse the MIME message so we search only decoded body text — NOT headers.
  // Headers commonly contain 6-digit runs (Message-IDs, DKIM signatures, timestamps)
  // that would otherwise produce false positives.
  const parsed = await simpleParser(message.source);
  const candidates = [parsed.text ?? '', stripHtml(parsed.html || '')];
  for (const body of candidates) {
    const match = SIX_DIGIT_CODE.exec(body);
    if (match) return match[0];
  }
  return undefined;
}

function stripHtml(html: string | false | undefined): string {
  if (!html) return '';
  return html.replace(/<[^>]+>/g, ' ');
}

function sleep(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}
