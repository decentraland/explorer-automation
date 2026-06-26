import { test } from '@playwright/test'
import { generatePrivateKey, privateKeyToAccount } from 'viem/accounts'
import { uniqueUsername } from '../helpers/test-user.js'
import { walletTest } from '../../../shared/fixtures/wallet-fixture.js'
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
import { generateFreshEmail, waitForOtp } from '../helpers/otp-mailbox.js'

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

walletTest.describe('@web @auth Creator Hub deep-link sign-in', () => {
  walletTest(
    'renders ContinueInApp with correct deep link and identity round-trip',
    async ({ page, ethereumWalletMock }) => {
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
      walletTest.expect(requestId).toBeTruthy()

      await installAutoWalletMockInitScript(page, account.address)
      await page.goto(`/auth/requests/${requestId}?targetConfigId=creator-hub&flow=deeplink`, { waitUntil: 'load' })
      await applyPersonalSignOverride(page)

      // The deep-link VerifySignIn view should NOT show a verification code
      // (deep-link flow hides it; only shows a confirmation prompt).
      const approveBtn = page.locator('[data-testid="verify-sign-in-approve-button"]')
      await approveBtn.waitFor({ state: 'visible', timeout: 30_000 })
      await walletTest.expect(page.locator('div').filter({ hasText: /^\d{4}$/ })).toBeHidden()

      // Intercept the POST /identities response to capture the identityId.
      const identityResponsePromise = page.waitForResponse(
        res => res.url().includes('/identities') && res.request().method() === 'POST' && res.status() === 200
      )

      await approveBtn.click()

      const identityResponse = await identityResponsePromise
      const identityBody = (await identityResponse.json()) as { identityId: string }
      walletTest.expect(identityBody.identityId).toBeTruthy()

      // The ContinueInApp view should appear with Creator Hub branding and
      // the dcl-creator-hub:// protocol in the deep-link URL.
      const returnBtn = page.locator('[data-testid="continue-in-app-return-button"]')
      await returnBtn.waitFor({ state: 'visible', timeout: 30_000 })
      await walletTest.expect(returnBtn).toContainText(/creator hub/i)

      // Verify identity round-trip: GET /identities/{identityId} returns the
      // identity that was just posted.
      const getRes = await fetch(`${authServerUrl()}/identities/${identityBody.identityId}`)
      walletTest.expect(getRes.ok).toBe(true)
      const identity = (await getRes.json()) as { address?: string }
      walletTest.expect(identity.address?.toLowerCase()).toBe(account.address.toLowerCase())
    }
  )

  walletTest(
    'deep-link flow works for new users without a registered profile',
    async ({ page, ethereumWalletMock }) => {
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
      walletTest.expect(requestId).toBeTruthy()

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
      await walletTest.expect(returnBtn).toContainText(/creator hub/i)
    }
  )
})

test('@web @auth email+OTP sign-in with Creator Hub deep link', async ({ page }) => {
  const email = generateFreshEmail()
  const landing = new LandingPage(page)
  const auth = new AuthPage(page)
  const qs = new QuickSetupPage(page)

  // Phase 1 — sign up a fresh user via email+OTP.
  await landing.goto()
  await landing.clickSignIn()
  await auth.submitEmail(email)
  await auth.waitForOtpScreen()
  const signupCode = await waitForOtp(email)
  await auth.enterOtp(signupCode)
  await qs.waitFor()
  await qs.fillUsername(uniqueUsername())
  await qs.acceptTerms()
  await qs.submit()
  await qs.clickStartExploring()
  await landing.waitForUrl()

  // Phase 2 — navigate to the RequestPage with Creator Hub deep-link params.
  // The Thirdweb InAppWallet from the OTP sign-in handles personal_sign natively.
  const ephemeralPrivateKey = generatePrivateKey()
  const ephemeralAccount = privateKeyToAccount(ephemeralPrivateKey)
  const expiration = new Date(Date.now() + 24 * 60 * 60 * 1000)
  const ephemeralMessage = getEphemeralMessage(ephemeralAccount.address, expiration)
  const { requestId } = await createAuthRequest('dcl_personal_sign', [ephemeralMessage])
  test.expect(requestId).toBeTruthy()

  await page.goto(`/auth/requests/${requestId}?targetConfigId=creator-hub&flow=deeplink`, { waitUntil: 'load' })

  const approveBtn = page.locator('[data-testid="verify-sign-in-approve-button"]')
  await approveBtn.waitFor({ state: 'visible', timeout: 30_000 })
  await test.expect(page.locator('div').filter({ hasText: /^\d{4}$/ })).toBeHidden()

  const identityResponsePromise = page.waitForResponse(
    res => res.url().includes('/identities') && res.request().method() === 'POST' && res.status() === 200
  )
  await approveBtn.click()

  const identityResponse = await identityResponsePromise
  const identityBody = (await identityResponse.json()) as { identityId: string }
  test.expect(identityBody.identityId).toBeTruthy()

  const returnBtn = page.locator('[data-testid="continue-in-app-return-button"]')
  await returnBtn.waitFor({ state: 'visible', timeout: 30_000 })
  await test.expect(returnBtn).toContainText(/creator hub/i)
})
