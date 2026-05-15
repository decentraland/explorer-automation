import type { Page, Request, Response } from '@playwright/test'

/**
 * Reactive observer over `/v1/transactions` POSTs against the transactions-server.
 *
 * Marketplace meta-tx flows (primary mint, accept listing) route the user's
 * EIP-712 signature to `/v1/transactions`; the server returns `{ txHash }` and
 * the relayer broadcasts on Polygon Amoy. First-time wallets fire **two** POSTs
 * — an approval signature followed by the buy/accept signature. Already-approved
 * wallets fire only one. Plain `page.waitForResponse` locks onto the first POST
 * (the approval), which is wrong for callers that want the buy/accept tx.
 *
 * Use this helper to attach listeners *before* triggering the dapp action,
 * then `await capture.waitFor(expectedPosts, timeoutMs)` once the spec knows
 * (via modal observation) how many POSTs to expect. The buy/accept response
 * is always the last entry in `responses`.
 *
 * The helper is deliberately UI-free: it observes the network only. Modal
 * driving belongs in the spec.
 */
export interface TransactionsCapture {
  /** All POST responses to `/v1/transactions` seen so far, in arrival order. */
  readonly responses: ReadonlyArray<Response>
  /** Count of POST requests attempted (may exceed `responses` if upstream is hung). */
  readonly requestsAttempted: number
  /**
   * Resolves once `count` POST responses have been observed. Throws on timeout
   * with a diagnostic that distinguishes "no request was attempted" (spec
   * didn't trigger the dapp saga) from "request fired but never received a
   * response" (transactions-api hung or rate-limiting).
   */
  waitFor(count: number, timeoutMs: number): Promise<void>
  /** Detach listeners. Must be called in spec teardown / finally. */
  dispose(): void
}

const TX_POST_RE = /\/v1\/transactions(\?|$)/

export function captureTransactionsPosts(page: Page): TransactionsCapture {
  const responses: Response[] = []
  let requestsAttempted = 0

  const onResponse = (res: Response): void => {
    if (TX_POST_RE.test(res.url()) && res.request().method() === 'POST') {
      responses.push(res)
    }
  }
  const onRequest = (req: Request): void => {
    if (TX_POST_RE.test(req.url()) && req.method() === 'POST') {
      requestsAttempted++
    }
  }

  page.on('response', onResponse)
  page.on('request', onRequest)

  return {
    get responses() {
      return responses
    },
    get requestsAttempted() {
      return requestsAttempted
    },
    async waitFor(count: number, timeoutMs: number): Promise<void> {
      const deadline = Date.now() + timeoutMs
      while (Date.now() < deadline && responses.length < count) {
        await new Promise<void>(resolve => setTimeout(resolve, 500))
      }
      if (responses.length < count) {
        const hint =
          requestsAttempted > responses.length
            ? ` ${requestsAttempted - responses.length} request(s) went out but never received a response — transactions-api may be hung or rate-limiting this wallet.`
            : ' No POST request was attempted — the spec likely did not trigger the dapp saga.'
        throw new Error(
          `Expected ${count} /v1/transactions POST(s), saw ${responses.length} response(s) (${requestsAttempted} request(s) attempted).${hint}`
        )
      }
    },
    dispose(): void {
      page.off('response', onResponse)
      page.off('request', onRequest)
    }
  }
}
