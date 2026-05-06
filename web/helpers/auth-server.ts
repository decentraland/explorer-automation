import { optionalEnv } from './env.js';
import type { AuthChain } from './identity.js';

/**
 * HTTP client for the Decentraland auth server (`https://auth-api.decentraland.org`).
 *
 * The auth server brokers signature requests between a desktop client (or any
 * out-of-band signer) and a wallet that lives in a browser session. The desktop
 * side POSTs a request describing what it wants signed and gets back a
 * `requestId`; it then surfaces a URL `https://decentraland.org/auth/requests/<id>`
 * to the user, who completes the signing in their browser. The desktop polls
 * the auth server for the outcome.
 *
 * Used by the RequestPage tests (`auth-request-page.spec.ts`) to simulate the
 * desktop side of that handshake.
 */

const DEFAULT_POLL_TIMEOUT_MS = 15_000;
const POLL_INTERVAL_MS = 1_000;

export interface CreateRequestResult {
  requestId: string;
  expiration: string;
  code: number;
}

export interface RequestOutcome {
  sender: string;
  requestId: string;
  result?: string;
  error?: { code: number; message: string };
}

export const AUTH_SERVER_URL =
  optionalEnv('AUTH_SERVER_URL') ?? 'https://auth-api.decentraland.org';

/**
 * Creates a new auth request on the server.
 *
 * @param method     The RPC method (e.g. `dcl_personal_sign`, `eth_sendTransaction`).
 * @param params     The method parameters.
 * @param authChain  Required for non-`dcl_personal_sign` methods.
 */
export async function createAuthRequest(
  method: string,
  params: unknown[],
  authChain?: AuthChain,
): Promise<CreateRequestResult> {
  const body: Record<string, unknown> = { method, params };
  if (authChain) body.authChain = authChain;

  const res = await fetch(`${AUTH_SERVER_URL}/requests`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  });
  if (!res.ok) {
    throw new Error(`createAuthRequest failed: ${res.status} ${await res.text()}`);
  }
  return (await res.json()) as CreateRequestResult;
}

/**
 * Polls the auth server for the outcome of a request. The server returns 204
 * while the request is still pending; once a wallet signs (or rejects), it
 * responds with the outcome JSON.
 */
export async function pollAuthOutcome(
  requestId: string,
  timeoutMs = DEFAULT_POLL_TIMEOUT_MS,
): Promise<RequestOutcome> {
  const deadline = Date.now() + timeoutMs;
  while (Date.now() < deadline) {
    const res = await fetch(`${AUTH_SERVER_URL}/requests/${requestId}`);
    if (res.status === 204) {
      await new Promise((r) => setTimeout(r, POLL_INTERVAL_MS));
      continue;
    }
    if (!res.ok) {
      throw new Error(`pollAuthOutcome failed: ${res.status} ${await res.text()}`);
    }
    return (await res.json()) as RequestOutcome;
  }
  throw new Error(`Polling for request ${requestId} timed out after ${timeoutMs}ms`);
}
