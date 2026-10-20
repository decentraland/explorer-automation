import { request as playwrightRequest } from '@playwright/test'
import type { APIRequestContext } from '@playwright/test'
// base-test only for rule compliance — this spec is API-only (no browser), so
// the CF-route context override never instantiates (fixtures are lazy)
import { test, expect } from '../../../shared/fixtures/base-test.js'
import { generatePrivateKey } from 'viem/accounts'
import { optionalEnv } from '../../../shared/helpers/env.js'
import { builderApiBaseUrl, buildSignedFetchHeaders } from '../../../shared/helpers/builder-api.js'

/**
 * Integration sweep for the builder-server authorization + disclosure fixes,
 * driven over real HTTP against a LOCAL server (builder-api.ts refuses any
 * non-local host). Two axes:
 *
 *   A. Public/foreign READS must not expose internal identifiers.
 *   B. State-mutating endpoints must reject an authenticated-but-unauthorized
 *      wallet — a freshly generated attacker with a valid signed auth chain.
 *
 * SCOPE / SAFETY: only non-destructive, middleware-gated mutations are exercised
 * live (publish, curation, tos, lock — each rejected by withCollectionAuthorization
 * / validateAccessToCuration BEFORE any write). Destructive verbs (DELETE
 * collection/item, PUT item) are deliberately NOT hammered against a real dev DB;
 * they share the same middleware and are covered by builder-server unit tests
 * (src/security.spec.ts, *.router.spec.ts).
 *
 * Requires BUILDER_API_URL (local) + VICTIM_TP_COLLECTION_ID (an existing,
 * published TP collection the attacker does NOT manage). Skips when unconfigured.
 */
const CID = optionalEnv('VICTIM_TP_COLLECTION_ID')
const configured = Boolean(optionalEnv('BUILDER_API_URL') && CID)

// created_at / updated_at are intentionally retained in the public DTOs (they
// are not exploitable and their absence produced NaN timestamps client-side).
const INTERNAL_FIELDS = ['id', 'salt', 'collection_id', 'local_content_hash', 'forum_id', 'forum_link', 'lock']

test.describe('@builder-api builder-server authorization', () => {
  test.skip(!configured, 'Set BUILDER_API_URL (local) and VICTIM_TP_COLLECTION_ID')

  const attackerKey = generatePrivateKey()
  let ctx: APIRequestContext
  let base: string

  test.beforeAll(async () => {
    base = builderApiBaseUrl()
    ctx = await playwrightRequest.newContext()
  })
  test.afterAll(async () => {
    await ctx.dispose()
  })

  // ── A. public / foreign reads must not leak internal identifiers ──────────

  test('public collections listing exposes no internal identifiers', async () => {
    const res = await ctx.get(`${base}/collections?is_published=true&q=a`)
    expect(res.status()).toBe(200)
    const results = (await res.json()).data as Array<Record<string, unknown>>
    expect(results.length).toBeGreaterThan(0)
    for (const c of results) {
      for (const f of INTERNAL_FIELDS) expect(c).not.toHaveProperty(f)
      expect(c).toHaveProperty('contract_address')
      expect(c).toHaveProperty('urn')
    }
  })

  // Note: GET /collections/:id single-read strip uses the same
  // toPublicCollection/canSeeCollection proven live by the item-list test below
  // and is unit-tested in Collection.router.spec.ts. It is not exercised here
  // because resolving a single TP collection needs its third party in the graph,
  // which prod third parties are not in a local env ("Third Party doesn't exist").

  test('a foreign wallet listing collection items gets published-only items (ids kept, internal fields stripped)', async () => {
    const path = `/collections/${CID}/items`
    const res = await ctx.get(`${base}${path}`, {
      headers: await buildSignedFetchHeaders(attackerKey, 'get', path)
    })
    expect(res.status()).toBe(200)
    const items = (await res.json()).data as Array<Record<string, unknown>>
    for (const item of items) {
      // id is kept on a targeted read (the client is already asking by
      // collection id, and every mutating route is authorization-gated); only
      // genuinely-internal fields are stripped, and drafts are hidden.
      expect(item).toHaveProperty('id')
      expect(item).not.toHaveProperty('local_content_hash')
      expect(item.is_published).toBe(true)
    }

    // A public reader's paginated total must be published-only (drafts excluded
    // from count), so it equals the number of published items returned above.
    // Guards the R1 regression where the count leaked the draft-inclusive total.
    const pRes = await ctx.get(`${base}${path}?limit=2&page=1`, {
      headers: await buildSignedFetchHeaders(attackerKey, 'get', path)
    })
    expect(pRes.status()).toBe(200)
    const pData = (await pRes.json()).data as {
      total: number
      results: Array<Record<string, unknown>>
    }
    for (const item of pData.results) {
      expect(item.is_published).toBe(true)
      expect(item).toHaveProperty('id')
    }
    expect(pData.total).toBe(items.length)
  })

  // ── B. state-mutating endpoints reject a foreign wallet, before any write ──

  const MUTATIONS = [
    {
      name: 'publish a TP collection',
      method: 'post' as const,
      path: `/collections/${CID}/publish`,
      body: {
        itemIds: ['00000000-0000-0000-0000-000000000000'],
        cheque: { signature: 'signature', qty: 1, salt: '0xsalt' }
      }
    },
    {
      name: 'send a collection to review (create curation)',
      method: 'post' as const,
      path: `/collections/${CID}/curation`,
      body: {}
    },
    {
      name: 'approve a collection curation',
      method: 'patch' as const,
      path: `/collections/${CID}/curation`,
      body: { curation: { status: 'approved' } }
    },
    {
      name: 'save the collection ToS',
      method: 'post' as const,
      path: `/collections/${CID}/tos`,
      body: { email: 'attacker@example.com' }
    },
    {
      name: 'lock a collection',
      method: 'post' as const,
      path: `/collections/${CID}/lock`,
      body: {}
    }
  ]

  for (const m of MUTATIONS) {
    test(`rejects a foreign wallet trying to ${m.name}`, async () => {
      const headers = {
        ...(await buildSignedFetchHeaders(attackerKey, m.method, m.path)),
        'content-type': 'application/json'
      }
      const res = await ctx[m.method](`${base}${m.path}`, { headers, data: m.body })
      expect(res.status()).toBe(401)
      // Must be the AUTHORIZATION rejection, not an auth-chain/signature failure
      // (a malformed signature also 401s, which would false-green).
      const body = JSON.stringify(await res.json())
      expect(body).not.toContain('verify')
      expect(body.toLowerCase()).toContain('unauthorized')
    })
  }
})
