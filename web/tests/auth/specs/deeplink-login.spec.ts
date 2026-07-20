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
import { AuthPage } from '../pages/AuthPage.js'
import { QuickSetupPage } from '../pages/QuickSetupPage.js'
import { LandingPage } from '../../landing/pages/LandingPage.js'
import {
  generateAuthRequestId,
  buildDeeplinkLoginPath,
  installDeeplinkCapture,
  waitForDeepLinkRedirect,
  parseDeepLinkUrl,
  fetchIdentity
} from '../helpers/deeplink.js'

/**
 * Deeplink login flow E2E tests.
 *
 * The Explorer desktop client opens the browser to
 * `/auth/requests/{uuid}?flow=deeplink` where {uuid} is a client-generated
 * UUID v4 correlation ID. Once the user is authenticated and their profile
 * is complete, the auth dapp:
 *   1. POSTs the identity to the auth server (`POST /identities`).
 *   2. Fires a deep link via a hidden iframe:
 *      `decentraland://open?signin={identityId}&authRequestId={uuid}`.
 *   3. The launcher forwards the deep link to the running Explorer instance.
 *   4. Explorer fetches the identity from `GET /identities/{identityId}`.
 *
 * These tests exercise the browser side by navigating to the deeplink request
 * URL, verifying the auth dapp produces a valid identity, and checking the
 * correct deep link redirect is attempted.
 *
 * Related PRs:
 *   - Launcher: github.com/decentraland/launcher-rust/pull/293
 *   - Explorer: github.com/decentraland/unity-explorer/pull/9100
 *   - Auth UI:  github.com/decentraland/auth/pull/430
 *   - Auth UI:  github.com/decentraland/auth/pull/431
 */

const { expect } = test
const REDIRECT_TO = `${getBaseUrl()}/`

test.describe('@web @auth deeplink login', () => {
  test('sign in via deeplink flow produces valid identity and correct deep link', async ({
    page,
    ethereumWalletMock
  }) => {
    const privateKey = generatePrivateKey()
    const account = privateKeyToAccount(privateKey)

    // ── Register a profile so the deeplink flow can proceed without setup ──
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

    // ── Drive the deeplink flow ──
    const authRequestId = generateAuthRequestId()

    // Install the deep link interceptor and auto-wallet-mock BEFORE navigating
    // so the init scripts are active when the auth dapp fires the redirect.
    await installDeeplinkCapture(page)
    await installAutoWalletMockInitScript(page, account.address)
    await page.goto(buildDeeplinkLoginPath(authRequestId), { waitUntil: 'load' })
    await applyPersonalSignOverride(page)

    // The auth dapp auto-posts the identity and fires the deep link when the
    // user is connected with a complete profile — no manual interaction needed.
    const deepLinkUrl = await waitForDeepLinkRedirect(page, 120_000)
    const params = parseDeepLinkUrl(deepLinkUrl)

    // The deep link must echo the authRequestId so the Explorer can match it
    // to the login that opened this browser flow.
    expect(params.authRequestId).toBe(authRequestId)
    expect(params.signin).toBeTruthy()

    // Verify the identity is retrievable from the auth server — the same call
    // the Explorer's DappDeepLinkAuthenticator makes.
    const identity = await fetchIdentity(params.signin!)
    expect(identity.authChain).toBeDefined()
    expect(identity.authChain.length).toBeGreaterThanOrEqual(2)

    // The SIGNER link in the auth chain must be the wallet address that signed.
    const signerLink = identity.authChain.find(link => link.type === 'SIGNER')
    expect(signerLink).toBeDefined()
    expect(signerLink!.payload.toLowerCase()).toBe(account.address.toLowerCase())
  })

  test('deeplink login identity contains valid ephemeral key and expiration', async ({ page, ethereumWalletMock }) => {
    const privateKey = generatePrivateKey()

    // ── Register a profile ──
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

    // ── Drive the deeplink flow ──
    const authRequestId = generateAuthRequestId()
    const account = privateKeyToAccount(privateKey)
    await installDeeplinkCapture(page)
    await installAutoWalletMockInitScript(page, account.address)
    await page.goto(buildDeeplinkLoginPath(authRequestId), { waitUntil: 'load' })
    await applyPersonalSignOverride(page)

    const deepLinkUrl = await waitForDeepLinkRedirect(page, 120_000)
    const params = parseDeepLinkUrl(deepLinkUrl)
    expect(params.signin).toBeTruthy()

    const identity = await fetchIdentity(params.signin!)

    // The identity must include an ephemeral key pair for signing subsequent
    // requests without re-prompting the wallet.
    expect(identity.ephemeralIdentity).toBeDefined()
    expect(identity.ephemeralIdentity.address).toMatch(/^0x[0-9a-fA-F]{40}$/)

    // The expiration must be in the future.
    const expiration = new Date(identity.expiration)
    expect(expiration.getTime()).toBeGreaterThan(Date.now())

    // The auth chain must have an ECDSA_EPHEMERAL link (the wallet's signature
    // granting authority to the ephemeral key).
    const ephemeralLink = identity.authChain.find(
      link => link.type === 'ECDSA_EPHEMERAL' || link.type === 'ECDSA_EIP_1654_EPHEMERAL'
    )
    expect(ephemeralLink).toBeDefined()
    expect(ephemeralLink!.signature).toMatch(/^0x[0-9a-fA-F]+$/)
  })
})
