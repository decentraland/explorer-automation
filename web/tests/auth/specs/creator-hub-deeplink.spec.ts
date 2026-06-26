import { generatePrivateKey, privateKeyToAccount } from 'viem/accounts'
import { uniqueUsername } from '../helpers/test-user.js'
import { walletTest as test } from '../../../shared/fixtures/wallet-fixture.js'
import {
  setupMockedWallet,
  mockNoProfileOnCatalysts,
  installAutoWalletMockInitScript,
  applyPersonalSignOverride
} from '../helpers/wallet.js'
import { getBaseUrl } from '../../../shared/helpers/env.js'
import { LandingPage } from '../../landing/pages/LandingPage.js'
import { AuthPage } from '../pages/AuthPage.js'
import { QuickSetupPage } from '../pages/QuickSetupPage.js'
import { createAuthRequest, authServerUrl } from '../helpers/auth-server.js'
import { getEphemeralMessage } from '../../../shared/helpers/identity.js'

/**
 * Creator Hub deep-link sign-in flow.
 *
 * The Creator Hub desktop app authenticates via the same RequestPage handshake
 * as the Explorer, but with two key differences:
 *   - `targetConfigId=creator-hub` — sets `skipSetup: true`, `deepLink: 'dcl-creator-hub://'`
 *   - `flow=deeplink` — identity is posted server-side; the auth dapp shows a
 *     ContinueInApp view that triggers `dcl-creator-hub://open?signin={identityId}`
 *     instead of auto-redirecting to a `decentraland://` deep link.
 *
 * These tests mirror `request-page.spec.ts` with the Creator Hub parameters.
 */

const REDIRECT_TO = `${getBaseUrl()}/`

const { expect } = test

test.describe('@web @auth Creator Hub deep-link sign-in', () => {
  test('renders ContinueInApp with correct deep link and identity round-trip', async ({ page, ethereumWalletMock }) => {
    const privateKey = generatePrivateKey()
    const account = privateKeyToAccount(privateKey)

    // Phase 1 — register a DCL profile so the RequestPage treats us as a
    // returning user (same bootstrap as request-page.spec.ts).
    const unmockProfile = await mockNoProfileOnCatalysts(page)
    await setupMockedWallet(page, ethereumWalletMock, { privateKey, redirectTo: REDIRECT_TO })
    await new AuthPage(page).clickMetaMaskButton()
    const qs = new QuickSetupPage(page)
    await qs.waitFor()
    await qs.fillUsername(uniqueUsername())
    await qs.acceptTerms()
    await qs.submit()
    await qs.clickStartExploring()
    await new LandingPage(page).waitForUrl()
    await unmockProfile()

    // Phase 2 — create an auth request and drive the RequestPage with
    // Creator Hub deep-link parameters.
    const ephemeralPrivateKey = generatePrivateKey()
    const ephemeralAccount = privateKeyToAccount(ephemeralPrivateKey)
    const expiration = new Date(Date.now() + 24 * 60 * 60 * 1000)
    const ephemeralMessage = getEphemeralMessage(ephemeralAccount.address, expiration)
    const { requestId } = await createAuthRequest('dcl_personal_sign', [ephemeralMessage])
    expect(requestId).toBeTruthy()

    await installAutoWalletMockInitScript(page, account.address)
    await page.goto(`/auth/requests/${requestId}?targetConfigId=creator-hub&flow=deeplink`, { waitUntil: 'load' })
    await applyPersonalSignOverride(page)

    // The deep-link VerifySignIn view should NOT show a verification code
    // (deep-link flow hides it; only shows a confirmation prompt).
    const approveBtn = page.locator('[data-testid="verify-sign-in-approve-button"]')
    await approveBtn.waitFor({ state: 'visible', timeout: 30_000 })
    await expect(page.locator('div').filter({ hasText: /^\d{4}$/ })).toBeHidden()

    // Intercept the POST /identities response to capture the identityId.
    const identityResponsePromise = page.waitForResponse(
      res => res.url().includes('/identities') && res.request().method() === 'POST' && res.status() === 200
    )

    await approveBtn.click()

    const identityResponse = await identityResponsePromise
    const identityBody = (await identityResponse.json()) as { identityId: string }
    expect(identityBody.identityId).toBeTruthy()

    // The ContinueInApp view should appear with Creator Hub branding and
    // the dcl-creator-hub:// protocol in the deep-link URL.
    const returnBtn = page.locator('[data-testid="continue-in-app-return-button"]')
    await returnBtn.waitFor({ state: 'visible', timeout: 30_000 })
    await expect(returnBtn).toContainText(/creator hub/i)

    // Verify identity round-trip: GET /identities/{identityId} returns the
    // identity that was just posted.
    const getRes = await fetch(`${authServerUrl()}/identities/${identityBody.identityId}`)
    expect(getRes.ok).toBe(true)
    const identity = (await getRes.json()) as { address?: string }
    expect(identity.address?.toLowerCase()).toBe(account.address.toLowerCase())
  })

  test('deep-link flow works for new users without a registered profile', async ({ page, ethereumWalletMock }) => {
    const privateKey = generatePrivateKey()
    const account = privateKeyToAccount(privateKey)

    // Phase 1 — register a profile (needed to establish wallet connection
    // state that persists into Phase 2).
    const unmockProfile = await mockNoProfileOnCatalysts(page)
    await setupMockedWallet(page, ethereumWalletMock, { privateKey, redirectTo: REDIRECT_TO })
    await new AuthPage(page).clickMetaMaskButton()
    const qs = new QuickSetupPage(page)
    await qs.waitFor()
    await qs.fillUsername(uniqueUsername())
    await qs.acceptTerms()
    await qs.submit()
    await qs.clickStartExploring()
    await new LandingPage(page).waitForUrl()
    // Keep profile mocked as missing — do NOT call unmockProfile().
    // This simulates a new Creator Hub user who hasn't registered a DCL
    // profile. With skipSetup=true (from creator-hub config), the RequestPage
    // skips the profile check entirely. With flow=deeplink, it also skips
    // auto-sign (which would otherwise fire for new users without deep-link).
    void unmockProfile

    // Phase 2 — navigate to the RequestPage with deep-link params. The user
    // appears to have no profile, but skipSetup=true skips the profile check
    // and flow=deeplink prevents auto-sign.
    const ephemeralPrivateKey = generatePrivateKey()
    const ephemeralAccount = privateKeyToAccount(ephemeralPrivateKey)
    const expiration = new Date(Date.now() + 24 * 60 * 60 * 1000)
    const ephemeralMessage = getEphemeralMessage(ephemeralAccount.address, expiration)
    const { requestId } = await createAuthRequest('dcl_personal_sign', [ephemeralMessage])
    expect(requestId).toBeTruthy()

    await installAutoWalletMockInitScript(page, account.address)
    await page.goto(`/auth/requests/${requestId}?targetConfigId=creator-hub&flow=deeplink`, { waitUntil: 'load' })
    await applyPersonalSignOverride(page)

    // VerifySignIn should appear (not auto-signed), proving that
    // flow=deeplink prevents the auto-sign path for new users.
    const approveBtn = page.locator('[data-testid="verify-sign-in-approve-button"]')
    await approveBtn.waitFor({ state: 'visible', timeout: 30_000 })

    const identityResponsePromise = page.waitForResponse(
      res => res.url().includes('/identities') && res.request().method() === 'POST' && res.status() === 200
    )
    await approveBtn.click()
    await identityResponsePromise

    const returnBtn = page.locator('[data-testid="continue-in-app-return-button"]')
    await returnBtn.waitFor({ state: 'visible', timeout: 30_000 })
    await expect(returnBtn).toContainText(/creator hub/i)
  })
})
