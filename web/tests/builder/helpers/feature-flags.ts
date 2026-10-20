import type { Page } from '@playwright/test'

interface FeatureFlagsPayload {
  flags: Record<string, boolean>
  variants: Record<string, unknown>
}

/**
 * Feature flags that fork which UI path the builder renders for the flows the
 * POMs drive. Pinned to the values observed on dev (2026-07-28: all absent from
 * https://feature-flags.decentraland.org/builder.json and dapps.json → OFF) so
 * a remote flip mid-run can't change which modal the POMs must operate:
 *
 *  - `builder-linked-wearables-payments` OFF → "Create Collection" opens
 *    CreateCollectionModal directly (no CreateCollectionSelectorModal), and
 *    third-party creation requires a pre-registered TP manager wallet.
 *  - `builder-linked-wearables-v2` OFF → TP creation modal shows the legacy
 *    "Id" field, no network/linked-contract fields (no live ERC-165 check).
 *  - `dapps-offchain-public-item-orders` OFF → on-sale toggle is
 *    "Put up for sale"/"Remove from marketplace" via setMinters(CollectionStore).
 *
 * When dev flips one of these permanently, the affected POM fails loudly and
 * the pin is updated deliberately — same philosophy as SYNPRESS_DEFAULT_KEY.
 * The fetch URL is hardcoded in decentraland-dapps
 * (dist/modules/features/utils.js: `https://feature-flags.decentraland.org/<app>.json`).
 */
export const PINNED_FLAGS: Readonly<Record<string, Readonly<Record<string, boolean>>>> = {
  builder: {
    'builder-linked-wearables-payments': false,
    'builder-linked-wearables-v2': false
  },
  dapps: {
    'dapps-offchain-public-item-orders': false
  }
}

/**
 * Route-intercepts the builder's feature-flag fetches and overlays PINNED_FLAGS
 * on the live payload. Register before the first `page.goto` — flags are read
 * once at app boot.
 */
export async function pinFeatureFlags(page: Page): Promise<void> {
  await page.route(/feature-flags\.decentraland\.(org|zone)\/(builder|dapps)\.json/, async route => {
    const app = new URL(route.request().url()).pathname.replace(/^\/|\.json$/g, '')
    const overrides = PINNED_FLAGS[app] ?? {}
    const response = await route.fetch()
    const payload = (await response.json()) as FeatureFlagsPayload
    await route.fulfill({
      response,
      json: { ...payload, flags: { ...payload.flags, ...overrides } }
    })
  })
}
