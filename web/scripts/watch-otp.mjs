// Helper for the codegen recording session.
// Polls the inbox and prints any OTP that arrives at the configured address.
// Usage:
//   node scripts/watch-otp.mjs                        # polls EXPLORER_IMAP_USER
//   node scripts/watch-otp.mjs you+abc@gmail.com      # polls a specific +alias
//
// Run this in a second terminal while playwright codegen records the login.
// As soon as the OTP email arrives, the 6-digit code prints to the console.

import { ImapFlow } from 'imapflow';
import { simpleParser } from 'mailparser';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import dotenv from 'dotenv';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
dotenv.config({ path: path.resolve(__dirname, '../../.env') });

const target = process.argv[2] ?? process.env.EXPLORER_IMAP_USER;
if (!target) {
  console.error('Set EXPLORER_IMAP_USER in .env or pass an address as the first arg.');
  process.exit(1);
}

const startedAt = Date.now();
console.log(`[otp-watch] watching for code addressed to ${target} from ${process.env.EXPLORER_IMAP_FROM_USER}`);
console.log('[otp-watch] press Ctrl+C to stop\n');

const client = new ImapFlow({
  host: process.env.EXPLORER_IMAP_HOST,
  port: Number(process.env.EXPLORER_IMAP_PORT),
  secure: true,
  auth: { user: process.env.EXPLORER_IMAP_USER, pass: process.env.EXPLORER_IMAP_PASSWORD },
  logger: false,
});
await client.connect();

const seen = new Set();

while (true) {
  const lock = await client.getMailboxLock('INBOX');
  try {
    const uids =
      (await client.search(
        { to: target, from: process.env.EXPLORER_IMAP_FROM_USER },
        { uid: true },
      )) || [];
    for (const uid of uids) {
      if (seen.has(uid)) continue;
      const msg = await client.fetchOne(String(uid), { source: true, envelope: true }, { uid: true });
      if (!msg) continue;
      const parsed = await simpleParser(msg.source);
      const text = parsed.text || (parsed.html || '').replace(/<[^>]+>/g, ' ');
      const match = /\d{6}/.exec(text);
      if (match) {
        const ts = new Date().toISOString().slice(11, 19);
        console.log(`\n[${ts}] OTP for ${target}: \x1b[1;32m${match[0]}\x1b[0m\n`);
      }
      seen.add(uid);
    }
  } finally {
    lock.release();
  }
  await new Promise((r) => setTimeout(r, 2_000));
  if (Date.now() - startedAt > 30 * 60 * 1000) {
    console.log('[otp-watch] 30min elapsed, exiting.');
    break;
  }
}

await client.logout();
