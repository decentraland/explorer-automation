import { privateKeyToAccount } from 'viem/accounts';

/**
 * Decentraland identity primitives — building blocks for the RequestPage flow.
 *
 * The DCL auth model uses a two-step identity:
 *   1. The "signer" wallet (your real address) signs an "ephemeral message"
 *      that delegates signing rights to a short-lived ephemeral key pair.
 *   2. Subsequent requests carry an "auth chain" — a list of `[SIGNER,
 *      ECDSA_EPHEMERAL]` segments — that the auth server uses to verify the
 *      user authorized the action. Only the ephemeral key signs operational
 *      requests, so the signer wallet doesn't get pinged for every action.
 *
 * Mirrors `auth-e2e-tests/src/logic/identity/`.
 */

export type AuthChainSegment =
  | { type: 'SIGNER'; payload: string; signature: '' }
  | { type: 'ECDSA_EPHEMERAL'; payload: string; signature: string };

export type AuthChain = AuthChainSegment[];

/**
 * Canonical format expected by the auth server for the ephemeral-grant message.
 * Changing the format invalidates the signature; keep the wording in sync with
 * the dapp + auth server.
 */
export function getEphemeralMessage(ephemeralAddress: string, expiration: Date): string {
  return `Decentraland Login\nEphemeral address: ${ephemeralAddress}\nExpiration: ${expiration.toISOString()}`;
}

/**
 * Builds the full auth chain for a `signerPrivateKey` granting authority to
 * `ephemeralAddress` until `expiration`. The signer signs the ephemeral
 * message with `personal_sign` (EIP-191).
 */
export async function buildAuthChain(
  signerPrivateKey: `0x${string}`,
  ephemeralAddress: string,
  expiration: Date,
): Promise<AuthChain> {
  const signer = privateKeyToAccount(signerPrivateKey);
  const message = getEphemeralMessage(ephemeralAddress, expiration);
  const signature = await signer.signMessage({ message });
  return [
    { type: 'SIGNER', payload: signer.address, signature: '' },
    { type: 'ECDSA_EPHEMERAL', payload: message, signature },
  ];
}
