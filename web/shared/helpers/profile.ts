import type { Page, Route } from '@playwright/test'

/**
 * Intercepts catalyst profile lookups and returns a fake "already onboarded"
 * profile for the given address. Without this, fresh wallets hit
 * `/auth/quick-setup` (the username/avatar onboarding form) instead of being
 * routed straight to the dapp homepage, because the auth dapp gates on
 * `profile.avatars[0].name !== undefined`.
 *
 * Call BEFORE `setupMockedWallet`. Returns an unmock function.
 */
export async function mockExistingProfile(page: Page, address: `0x${string}`): Promise<() => Promise<void>> {
  const lower = address.toLowerCase()
  const fakeAvatar = {
    userId: lower,
    ethAddress: lower,
    name: `QA${lower.slice(2, 8)}`,
    hasClaimedName: false,
    version: 1,
    tutorialStep: 256,
    description: '',
    unclaimedName: `QA${lower.slice(2, 8)}`,
    avatar: {
      bodyShape: 'urn:decentraland:off-chain:base-avatars:BaseFemale',
      eyes: { color: { r: 0.23, g: 0.62, b: 0.77 } },
      hair: { color: { r: 0.35, g: 0.19, b: 0.05 } },
      skin: { color: { r: 0.94, g: 0.76, b: 0.65 } },
      wearables: [
        'urn:decentraland:off-chain:base-avatars:f_eyes_00',
        'urn:decentraland:off-chain:base-avatars:f_eyebrows_00',
        'urn:decentraland:off-chain:base-avatars:f_mouth_00'
      ],
      snapshots: { face256: '', body: '' }
    }
  }
  const profileEnvelope = { avatars: [fakeAvatar] }

  const profilesMatcher = (url: URL): boolean => /\/lambdas\/profiles?(\/0x[a-f0-9]{40})?\/?$/i.test(url.pathname)

  const handler = async (route: Route): Promise<void> => {
    const req = route.request()
    const url = new URL(req.url())
    if (req.method() === 'POST') {
      // POST /lambdas/profiles with { ids: ["0x..."] } → array
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([profileEnvelope])
      })
      return
    }
    // GET /lambdas/profiles/<address> → single envelope
    // GET /lambdas/profile/<address>  → also single envelope (legacy path)
    if (/\/lambdas\/profiles?\/0x[a-f0-9]{40}/i.test(url.pathname)) {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(profileEnvelope)
      })
      return
    }
    await route.continue()
  }

  await page.route(profilesMatcher, handler)
  return async () => {
    await page.unroute(profilesMatcher, handler)
  }
}
