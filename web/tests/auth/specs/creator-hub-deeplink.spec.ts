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
 *   - `flow=deeplink` — identity is posted server-side (`POST /identities` →
 *     `201`); the auth dapp then shows the ContinueInApp view, which triggers
 *     `dcl-creator-hub://open?signin={identityId}` instead of auto-redirecting
 *     to a `decentraland://` deep link.
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
      // The deep-link flow posts the identity server-side and the endpoint
      // returns 201 (Created).
      const identityResponsePromise = page.waitForResponse(
        res => res.url().includes('/identities') && res.request().method() === 'POST' && res.status() === 201
      )

      await approveBtn.click()

      const identityResponse = await identityResponsePromise
      const identityBody = (await identityResponse.json()) as { identityId: string }
      walletTest.expect(identityBody.identityId).toBeTruthy()

      // The ContinueInApp view should render (it triggers the dcl-creator-hub://
      // deep link on mount). Without the desktop app installed it lands on the
      // "Could not open Creator Hub" terminal state — assert on its stable
      // control instead of a success button that never appears in CI/dev.
      const continueInAppBtn = page.locator('[data-testid="continue-in-app-go-back-button"]')
      await continueInAppBtn.waitFor({ state: 'visible', timeout: 30_000 })

      // Verify identity round-trip: GET /identities/{identityId} returns the
      // identity that was just posted. The signer address isn't a top-level
      // field — it's the SIGNER segment's payload in the returned auth chain:
      //   { identity: { ephemeralIdentity, expiration, authChain: [{ type: 'SIGNER', payload: <address> }, ...] } }
      const getRes = await fetch(`${authServerUrl()}/identities/${identityBody.identityId}`)
      walletTest.expect(getRes.ok).toBe(true)
      const { identity } = (await getRes.json()) as {
        identity: { authChain: { type: string; payload: string }[] }
      }
      const signer = identity.authChain.find(seg => seg.type === 'SIGNER')
      walletTest.expect(signer?.payload.toLowerCase()).toBe(account.address.toLowerCase())
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
        res => res.url().includes('/identities') && res.request().method() === 'POST' && res.status() === 201
      )
      await approveBtn.click()
      await identityResponsePromise

      // ContinueInApp view reached (see file header for why we assert on the
      // go-back control rather than a success button).
      const continueInAppBtn = page.locator('[data-testid="continue-in-app-go-back-button"]')
      await continueInAppBtn.waitFor({ state: 'visible', timeout: 30_000 })
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
    res => res.url().includes('/identities') && res.request().method() === 'POST' && res.status() === 201
  )
  await approveBtn.click()

  const identityResponse = await identityResponsePromise
  const identityBody = (await identityResponse.json()) as { identityId: string }
  test.expect(identityBody.identityId).toBeTruthy()

  // ContinueInApp view reached (see file header for why we assert on the
  // go-back control rather than a success button).
  const continueInAppBtn = page.locator('[data-testid="continue-in-app-go-back-button"]')
  await continueInAppBtn.waitFor({ state: 'visible', timeout: 30_000 })
})
