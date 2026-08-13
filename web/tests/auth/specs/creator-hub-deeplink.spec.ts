import { test, type Page } from '@playwright/test'
import { generatePrivateKey, privateKeyToAccount } from 'viem/accounts'
import type { EthereumWalletMock } from '@synthetixio/ethereum-wallet-mock/playwright'
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
 * What we assert — and why not the "success" UI. The ContinueInApp view fires
 * the `dcl-creator-hub://` protocol as soon as it mounts. On any machine
 * WITHOUT the Creator Hub desktop app installed (every CI runner and dev box),
 * the OS can't open the protocol and the view lands on its terminal
 * `continue-in-app-go-back-button` state ("Could not open Creator Hub"). There
 * is no environment-independent "return to app succeeded" signal to wait on, so
 * these tests verify the auth CONTRACT instead: the identity round-trip
 * (`POST /identities` → `201`, then `GET /identities/{id}` returns the signer's
 * address) plus reaching the ContinueInApp view (proving the deep-link stage
 * rendered rather than bouncing to /auth/login).
 *
 * These tests mirror `request-page.spec.ts` with the Creator Hub parameters.
 * The step helpers below are intentionally file-local; a future PR can promote
 * the shared web3-bootstrap / request-minting pieces into `tests/auth/helpers/`
 * once request-page.spec.ts is migrated to reuse them too.
 */

const REDIRECT_TO = `${getBaseUrl()}/`
const DEEP_LINK_REQUEST_PATH = (requestId: string): string =>
  `/auth/requests/${requestId}?targetConfigId=creator-hub&flow=deeplink`

// English copy rendered by the auth dapp's VerifySignIn view (auth repo:
// src/components/Pages/RequestPage/Views/VerifySignIn/VerifySignIn.tsx +
// src/modules/translations/en.json). The verification-code block
// (`request.verification_match` + <VerificationCode>) is rendered ONLY in the
// non-deep-link branch; the deep-link branch shows `request.deep_link_confirm`
// instead. Asserting on these two phrases is how we prove the code-confirmation
// UI is absent — the code element itself has no data-testid to target.
const VERIFICATION_MATCH_TEXT = /Does the verification number below match/i
const DEEP_LINK_CONFIRM_TEXT = /Please confirm you want to sign in/i

/**
 * Phase 1 — sign up a fresh web3 user through the browser auth flow so a wallet
 * connection is established for the RequestPage phase. Returns the profile
 * unmock function; the caller decides whether to call it (returning-user tests
 * unmock so the dapp sees a profile; the new-user test leaves it mocked-missing).
 *
 * Note the split between backend and dapp view: signup DOES register a profile
 * on the catalysts, but `mockNoProfileOnCatalysts` intercepts the dapp's own
 * profile lookups. So "unmock" controls what the DAPP sees, independent of what
 * the real backend stores.
 */
async function bootstrapWeb3Profile(
  page: Page,
  ethereumWalletMock: EthereumWalletMock,
  privateKey: `0x${string}`
): Promise<() => Promise<void>> {
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
  return unmockProfile
}

/**
 * Simulates the desktop side of the handshake: mint a fresh ephemeral key pair
 * and ask the auth server for a `dcl_personal_sign` request for its ephemeral
 * message. Returns the requestId (the caller asserts it is non-empty).
 *
 * Helpers here perform ACTIONS and return values/locators only; assertions stay
 * in the test bodies (Playwright's `no-standalone-expect` / `expect-expect`
 * rules, and the repo convention that specs assert while helpers interact).
 */
async function mintDeepLinkRequest(): Promise<string> {
  const ephemeralAccount = privateKeyToAccount(generatePrivateKey())
  const expiration = new Date(Date.now() + 24 * 60 * 60 * 1000)
  const ephemeralMessage = getEphemeralMessage(ephemeralAccount.address, expiration)
  const { requestId } = await createAuthRequest('dcl_personal_sign', [ephemeralMessage])
  return requestId
}

/**
 * Locators for the deep-link VerifySignIn view. `approveBtn` is the sign-in
 * confirm button; `confirmPrompt` is the deep-link confirmation copy (shown in
 * this flow); `verificationMatchPrompt` is the "does the number match?" copy
 * that accompanies the numeric code (shown ONLY in the non-deep-link flow, so
 * the caller asserts it hidden to prove the code UI is absent). Lazy — the
 * caller waits/asserts.
 */
function deepLinkVerifyLocators(page: Page): {
  approveBtn: ReturnType<Page['locator']>
  confirmPrompt: ReturnType<Page['getByText']>
  verificationMatchPrompt: ReturnType<Page['getByText']>
} {
  return {
    approveBtn: page.locator('[data-testid="verify-sign-in-approve-button"]'),
    confirmPrompt: page.getByText(DEEP_LINK_CONFIRM_TEXT),
    verificationMatchPrompt: page.getByText(VERIFICATION_MATCH_TEXT)
  }
}

/** ContinueInApp go-back control — renders in both the success and "Could not
 * open Creator Hub" terminal states (see file header). Lazy locator. */
function continueInAppButton(page: Page): ReturnType<Page['locator']> {
  return page.locator('[data-testid="continue-in-app-go-back-button"]')
}

/**
 * Clicks approve, awaits the server-side `POST /identities` (201), and returns
 * the created identityId (the caller asserts it is non-empty).
 */
async function approveAndCaptureIdentity(
  page: Page,
  approveBtn: ReturnType<Page['locator']>
): Promise<string> {
  const identityResponsePromise = page.waitForResponse(
    res => res.url().includes('/identities') && res.request().method() === 'POST' && res.status() === 201,
    { timeout: 30_000 }
  )
  await approveBtn.click()
  const identityResponse = await identityResponsePromise
  const identityBody = (await identityResponse.json()) as { identityId: string }
  return identityBody.identityId
}

walletTest.describe('@web @auth Creator Hub deep-link sign-in', () => {
  walletTest(
    'renders ContinueInApp with correct deep link and identity round-trip',
    async ({ page, ethereumWalletMock }) => {
      const privateKey = generatePrivateKey()
      const account = privateKeyToAccount(privateKey)

      // Phase 1 — register a DCL profile so the RequestPage treats us as a
      // returning user (same bootstrap as request-page.spec.ts). Unmock so the
      // dapp finds the profile.
      const unmockProfile = await bootstrapWeb3Profile(page, ethereumWalletMock, privateKey)
      await unmockProfile()

      // Phase 2 — create an auth request and drive the RequestPage with
      // Creator Hub deep-link parameters.
      const requestId = await mintDeepLinkRequest()
      walletTest.expect(requestId).toBeTruthy()
      await installAutoWalletMockInitScript(page, account.address)
      await page.goto(DEEP_LINK_REQUEST_PATH(requestId), { waitUntil: 'load' })
      await applyPersonalSignOverride(page)

      // Deep-link VerifySignIn view: confirmation prompt shown, code-match
      // prompt (which accompanies the numeric code) absent.
      const { approveBtn, confirmPrompt, verificationMatchPrompt } = deepLinkVerifyLocators(page)
      await approveBtn.waitFor({ state: 'visible', timeout: 30_000 })
      await walletTest.expect(confirmPrompt).toBeVisible()
      await walletTest.expect(verificationMatchPrompt).toBeHidden()

      const identityId = await approveAndCaptureIdentity(page, approveBtn)
      walletTest.expect(identityId).toBeTruthy()
      await walletTest.expect(continueInAppButton(page)).toBeVisible({ timeout: 30_000 })

      // Verify identity round-trip: GET /identities/{identityId} returns the
      // identity that was just posted. The signer address isn't a top-level
      // field — it's the SIGNER segment's payload in the returned auth chain:
      //   { identity: { ephemeralIdentity, expiration, authChain: [{ type: 'SIGNER', payload: <address> }, ...] } }
      const getRes = await fetch(`${authServerUrl()}/identities/${identityId}`)
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

      // Phase 1 — run the full web3 signup (needed to establish the wallet
      // connection that persists into Phase 2), but deliberately DO NOT unmock:
      // the dapp keeps seeing a 404 profile lookup, so from the RequestPage's
      // perspective this is a brand-new Creator Hub user with no DCL profile.
      // (The signup does register a profile on the catalysts; the mock only
      // affects what the dapp reads.) With skipSetup=true (creator-hub config)
      // the RequestPage skips the profile check, and flow=deeplink prevents the
      // auto-sign that would otherwise fire for a new user.
      await bootstrapWeb3Profile(page, ethereumWalletMock, privateKey)

      // Phase 2 — navigate to the RequestPage with deep-link params.
      const requestId = await mintDeepLinkRequest()
      walletTest.expect(requestId).toBeTruthy()
      await installAutoWalletMockInitScript(page, account.address)
      await page.goto(DEEP_LINK_REQUEST_PATH(requestId), { waitUntil: 'load' })
      await applyPersonalSignOverride(page)

      // VerifySignIn should appear (not auto-signed), proving that flow=deeplink
      // prevents the auto-sign path for new users. Deep-link confirm variant:
      // confirmation prompt shown, code-match prompt absent.
      const { approveBtn, confirmPrompt, verificationMatchPrompt } = deepLinkVerifyLocators(page)
      await approveBtn.waitFor({ state: 'visible', timeout: 30_000 })
      await walletTest.expect(confirmPrompt).toBeVisible()
      await walletTest.expect(verificationMatchPrompt).toBeHidden()

      const identityId = await approveAndCaptureIdentity(page, approveBtn)
      walletTest.expect(identityId).toBeTruthy()
      await walletTest.expect(continueInAppButton(page)).toBeVisible({ timeout: 30_000 })
    }
  )
})

test('@web @auth email+OTP sign-in with Creator Hub deep link', async ({ page }) => {
  const email = generateFreshEmail()
  const landing = new LandingPage(page)
  const auth = new AuthPage(page)
  const qs = new QuickSetupPage(page)

  // Phase 1 — sign up a fresh user via email+OTP. (Not shared with the web3
  // bootstrap helper: this path drives the OTP screens, not the wallet mock.)
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
  const requestId = await mintDeepLinkRequest()
  test.expect(requestId).toBeTruthy()
  await page.goto(DEEP_LINK_REQUEST_PATH(requestId), { waitUntil: 'load' })

  // Deep-link VerifySignIn view: confirmation prompt shown, code-match prompt
  // (which accompanies the numeric code) absent.
  const { approveBtn, confirmPrompt, verificationMatchPrompt } = deepLinkVerifyLocators(page)
  await approveBtn.waitFor({ state: 'visible', timeout: 30_000 })
  await test.expect(confirmPrompt).toBeVisible()
  await test.expect(verificationMatchPrompt).toBeHidden()

  const identityId = await approveAndCaptureIdentity(page, approveBtn)
  test.expect(identityId).toBeTruthy()
  await test.expect(continueInAppButton(page)).toBeVisible({ timeout: 30_000 })
})
