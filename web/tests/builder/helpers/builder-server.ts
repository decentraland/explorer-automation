import { privateKeyToAddress } from 'viem/accounts'
import { getBaseUrl } from '../../../shared/helpers/env.js'
import { buildSignedFetchHeaders } from '../../../shared/helpers/builder-api.js'

/**
 * builder-server host for the DEPLOYED environment the UI suite runs against
 * (seeding, cleanup, state polling). Deliberately separate from
 * `shared/helpers/builder-api.ts`'s `builderApiBaseUrl()`, whose local-only
 * interlock guards the exploit-replay spec and must stay untouched.
 *
 * Resolution rule (marketplace-api.ts pattern, read at call time):
 *
 *  1. `BUILDER_SERVER_URL` env var — explicit override, used verbatim
 *     (trailing slash stripped, `/v1` suffix stripped if present).
 *  2. Host is `decentraland.today` — throw. The builder suite targets dev only
 *     (the only env with curation privileges); `.today` pairs with prod data.
 *  3. Host is `decentraland.org` with `MARKETPLACE_ENV=dev` (the default) —
 *     `https://builder-api.decentraland.zone`, matching what the dapp itself
 *     resolves when booted with `?env=dev`.
 *  4. Otherwise derive `builder-api.<host>`.
 *
 * Returns the host WITHOUT the `/v1` API-version prefix — request URLs add it,
 * while signed paths must omit it (see buildSignedFetchHeaders).
 */
export function builderServerBaseUrl(): string {
  const explicit = process.env.BUILDER_SERVER_URL
  if (explicit) return explicit.replace(/\/+$/, '').replace(/\/v1$/, '')

  const host = new URL(getBaseUrl()).host
  const env = process.env.MARKETPLACE_ENV ?? 'dev'

  if (host === 'decentraland.today') {
    throw new Error(
      'Builder tests do not support WEB_BASE_URL=https://decentraland.today — the suite targets ' +
        'the dev builder-server (the only env with curation privileges). Use ' +
        'WEB_BASE_URL=https://decentraland.org with MARKETPLACE_ENV=dev, or decentraland.zone.'
    )
  }

  if (host === 'decentraland.org' && env === 'dev') {
    return 'https://builder-api.decentraland.zone'
  }

  return `https://builder-api.${host}`
}

interface BuilderServerResponse<T> {
  ok: boolean
  data: T
  error?: string
}

/**
 * Signed request against the deployed builder-server. `path` is the route
 * WITHOUT the `/v1` prefix (the auth chain signs `<method>:<path>` in that
 * form even though the request URL includes `/v1`).
 */
async function signedRequest<T>(privateKey: `0x${string}`, method: string, path: string): Promise<T> {
  const headers = await buildSignedFetchHeaders(privateKey, method, path)
  const response = await fetch(`${builderServerBaseUrl()}/v1${path}`, { method, headers })
  const text = await response.text()
  const body = (text ? JSON.parse(text) : { ok: response.ok, data: undefined }) as BuilderServerResponse<T>
  if (!response.ok || !body.ok) {
    throw new Error(`builder-server ${method} ${path} failed (${response.status}): ${body.error ?? 'unknown error'}`)
  }
  return body.data
}

export interface RemoteCollection {
  id: string
  name: string
  is_published: boolean
  is_approved: boolean
  created_at: string
}

export interface RemoteItem {
  id: string
  name: string
}

export interface RemoteCuration {
  id: string
  collection_id: string
  status: 'pending' | 'approved' | 'rejected'
}

export async function getCollection(privateKey: `0x${string}`, collectionId: string): Promise<RemoteCollection> {
  return signedRequest<RemoteCollection>(privateKey, 'GET', `/collections/${collectionId}`)
}

/** Latest CollectionCuration for the collection — null when none exists yet. */
export async function getCuration(privateKey: `0x${string}`, collectionId: string): Promise<RemoteCuration | null> {
  return signedRequest<RemoteCuration | null>(privateKey, 'GET', `/collections/${collectionId}/curation`)
}

/** Committee member addresses per the builder-server (mirrors the on-chain Committee). */
export async function getCommittee(privateKey: `0x${string}`): Promise<string[]> {
  const data = await signedRequest<Array<{ address: string }>>(privateKey, 'GET', '/committee')
  return data.map(member => member.address.toLowerCase())
}

/** Collections owned by the key's address (includes unpublished — signed read). */
export async function getUserCollections(privateKey: `0x${string}`): Promise<RemoteCollection[]> {
  const address = privateKeyToAddress(privateKey).toLowerCase()
  const data = await signedRequest<RemoteCollection[] | { results: RemoteCollection[] }>(
    privateKey,
    'GET',
    `/${address}/collections`
  )
  return Array.isArray(data) ? data : data.results
}

export async function getCollectionItems(privateKey: `0x${string}`, collectionId: string): Promise<RemoteItem[]> {
  const data = await signedRequest<RemoteItem[] | { results: RemoteItem[] }>(
    privateKey,
    'GET',
    `/collections/${collectionId}/items`
  )
  return Array.isArray(data) ? data : data.results
}

export async function deleteItem(privateKey: `0x${string}`, itemId: string): Promise<void> {
  await signedRequest(privateKey, 'DELETE', `/items/${itemId}`)
}

export async function deleteCollection(privateKey: `0x${string}`, collectionId: string): Promise<void> {
  await signedRequest(privateKey, 'DELETE', `/collections/${collectionId}`)
}

/** Items first, then the collection (the server rejects deleting non-empty collections). */
export async function deleteCollectionCascade(privateKey: `0x${string}`, collectionId: string): Promise<void> {
  const items = await getCollectionItems(privateKey, collectionId)
  for (const item of items) {
    await deleteItem(privateKey, item.id)
  }
  await deleteCollection(privateKey, collectionId)
}

const qaPrefix = () => process.env.TEST_COLLECTION_PREFIX ?? 'QA'

/**
 * Best-effort teardown for EPHEMERAL test wallets: deletes every unpublished
 * collection the wallet owns, items first. Failures are logged, not thrown —
 * leftover rows on dev are prefixed garbage, not a test failure. Never use
 * with a shared/fixed wallet (it would take manual collections with it) —
 * that's what sweepStaleQaCollections is for.
 */
export async function cleanupUserCollections(privateKey: `0x${string}`): Promise<void> {
  try {
    const collections = await getUserCollections(privateKey)
    for (const collection of collections.filter(c => !c.is_published)) {
      await deleteCollectionCascade(privateKey, collection.id)
    }
  } catch (error) {
    console.warn(`builder cleanup skipped: ${error instanceof Error ? error.message : String(error)}`)
  }
}

/**
 * Crash-recovery sweep for the SHARED test wallet: deletes unpublished,
 * QA-prefixed collections older than `olderThanMs` (default 1h). The age gate
 * protects collections that a concurrently running suite (same wallet,
 * another worker/machine) is actively using; the prefix gate protects any
 * manually created collections on the wallet.
 */
export async function sweepStaleQaCollections(privateKey: `0x${string}`, olderThanMs = 60 * 60 * 1000): Promise<void> {
  try {
    const collections = await getUserCollections(privateKey)
    const cutoff = Date.now() - olderThanMs
    const stale = collections.filter(
      c => !c.is_published && c.name.startsWith(qaPrefix()) && new Date(c.created_at).getTime() < cutoff
    )
    for (const collection of stale) {
      await deleteCollectionCascade(privateKey, collection.id)
    }
  } catch (error) {
    console.warn(`builder stale sweep skipped: ${error instanceof Error ? error.message : String(error)}`)
  }
}

/**
 * Best-effort teardown for orphan items (created outside any collection) owned
 * by an ephemeral test wallet.
 */
export async function cleanupUserItems(privateKey: `0x${string}`): Promise<void> {
  try {
    const address = privateKeyToAddress(privateKey).toLowerCase()
    const data = await signedRequest<RemoteItem[] | { results: RemoteItem[] }>(privateKey, 'GET', `/${address}/items`)
    const items = Array.isArray(data) ? data : data.results
    for (const item of items) {
      await deleteItem(privateKey, item.id)
    }
  } catch (error) {
    console.warn(`builder item cleanup skipped: ${error instanceof Error ? error.message : String(error)}`)
  }
}
