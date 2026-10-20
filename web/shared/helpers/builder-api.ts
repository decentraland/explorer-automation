import { generatePrivateKey, privateKeyToAccount } from 'viem/accounts'
import { getEphemeralMessage } from './identity.js'

/**
 * Builder-server host under test. Defaults to a LOCAL server and REFUSES any
 * decentraland.org/zone/today host.
 *
 * Why the refusal is not optional: the publish-authorization spec performs the
 * exact unauthorized action from the vulnerability report. Against a server
 * that lacks the fix, that request SUCCEEDS and writes malicious state onto a
 * real collection — i.e. running it against a shared/prod host is running the
 * exploit. It may only ever hit a local build of the fixed server.
 */
export function builderApiBaseUrl(): string {
  const raw = process.env.BUILDER_API_URL
  if (!raw) {
    throw new Error('BUILDER_API_URL is not set. Point it at a LOCAL builder-server, e.g. http://127.0.0.1:5000/v1')
  }
  const url = new URL(raw)
  const host = url.hostname
  const isLocal = host === 'localhost' || host === '127.0.0.1' || host.endsWith('.local')
  if (!isLocal) {
    throw new Error(
      `Refusing to run builder authorization tests against non-local host "${host}". ` +
        'These tests attempt the unauthorized publish from the vulnerability report; ' +
        'against a server without the fix they would write real state. Use a local server.'
    )
  }
  return raw.replace(/\/+$/, '')
}

/**
 * Builds the `x-identity-auth-chain-*` headers a Decentraland server expects for
 * a signed request, for `method`+`path` signed by `userPrivateKey`.
 *
 * Mirrors `@dcl/crypto` `Authenticator.signPayload`: a 3-link chain where the
 * ephemeral key signs the `"<method>:<path>"` endpoint. This is the format the
 * builder-server's deprecated `validateSignature` path accepts (the modern
 * `verify` path, tried first, needs timestamp+metadata and is expected to fall
 * through). Verified against a live local server: the accepted endpoint is
 * `<method>:<path>` WITHOUT the API-version prefix — pass `/collections/:id/...`,
 * NOT `/v1/collections/:id/...`, even though the request URL includes `/v1`.
 *
 * The signer here is a genuinely different wallet than any collection owner —
 * an authenticated-but-unauthorized attacker, exactly the report's scenario.
 */
export async function buildSignedFetchHeaders(
  userPrivateKey: `0x${string}`,
  method: string,
  /** Route path WITHOUT the `/v1` API-version prefix. */
  path: string
): Promise<Record<string, string>> {
  const user = privateKeyToAccount(userPrivateKey)
  const ephemeral = privateKeyToAccount(generatePrivateKey())

  const expiration = new Date()
  expiration.setMinutes(expiration.getMinutes() + 30)
  const grantMessage = getEphemeralMessage(ephemeral.address, expiration)
  const grantSignature = await user.signMessage({ message: grantMessage })

  const endpoint = `${method}:${path}`.toLowerCase()
  const endpointSignature = await ephemeral.signMessage({ message: endpoint })

  const authChain = [
    { type: 'SIGNER', payload: user.address, signature: '' },
    { type: 'ECDSA_EPHEMERAL', payload: grantMessage, signature: grantSignature },
    { type: 'ECDSA_SIGNED_ENTITY', payload: endpoint, signature: endpointSignature }
  ]

  const headers: Record<string, string> = {}
  authChain.forEach((link, i) => {
    headers[`x-identity-auth-chain-${i}`] = JSON.stringify(link)
  })
  return headers
}
