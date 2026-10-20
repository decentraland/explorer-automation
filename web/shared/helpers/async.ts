/**
 * Resolves after `ms` milliseconds. The one timing primitive shared across the
 * cross-stack helpers (`auth-request-bridge`, `token-bridge`, `explorer-runner`,
 * `otp-mailbox`) that poll disk / ports / IMAP on an interval.
 *
 * This is for polling *external* state the stack genuinely can't be notified
 * about (a file Unity writes, a TCP listener AltTester opens, an IMAP inbox).
 * It is NOT a substitute for Playwright's auto-waiting inside a page flow —
 * never `sleep()` instead of `waitFor` / `waitForURL` in a spec body.
 */
export function sleep(ms: number): Promise<void> {
  return new Promise(resolve => setTimeout(resolve, ms))
}
