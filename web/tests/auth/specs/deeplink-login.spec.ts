import { generatePrivateKey, privateKeyToAccount } from 'viem/accounts'
import { uniqueUsername } from '../helpers/test-user.js'
import { walletTest as test } from '../../../shared/fixtures/wallet-fixture.js'
import { setupMockedWallet, mockNoProfileOnCatalysts, applyPersonalSignOverride } from '../helpers/wallet.js'
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
 * The Explorer desktop client uses a browser-based deep link flow for wallet
 * sign-in (replacing the old socket-based code-verification flow):
 *
 *   1. Explorer generates a client-side UUID (`authRequestId`).
 *   2. Explorer opens the browser to
 *      `/auth/login/{authRequestId}?loginMethod=metamask&flow=deeplink`.
 *   3. The user connects their wallet and completes sign-in in the browser.
 *   4. The auth dapp creates an identity on the auth server, then redirects
 *      via `decentraland://?signin={identityId}&authRequestId={authRequestId}`.
 *   5. The launcher forwards the deep link to the running Explorer instance.
 *   6. Explorer fetches the identity from `GET /identities/{identityId}`.
 *
 * These tests exercise the browser side (steps 2–4) by navigating to the
 * deeplink login URL, completing the wallet flow, and verifying the auth dapp
 * produces a valid identity and attempts the correct deep link redirect.
 *
 * Related PRs:
 *   - Launcher: github.com/decentraland/launcher-rust/pull/293
 *   - Explorer: github.com/decentraland/unity-explorer/pull/9100
 */

const { expect } = test
const REDIRECT_TO = `${getBaseUrl()}/`

test.describe('@web @auth deeplink login', () => {
  test('new user can sign in via deeplink flow', async ({ page, ethereumWalletMock }) => {
    const privateKey = generatePrivateKey()
    const account = privateKeyToAccount(privateKey)
    const authRequestId = generateAuthRequestId()

    // Install the deep link redirect interceptor BEFORE any navigation so
    // the init script is active when the auth dapp fires the redirect.
    await installDeeplinkCapture(page)

    // Mock a missing profile so the dapp routes through quick-setup (new user).
    const unmockProfile = await mockNoProfileOnCatalysts(page)

    // Set up wallet plumbing (polyfill + signer) via setupMockedWallet, which
    // navigates to /auth/login. The wallet init scripts persist across
    // navigations.
    await setupMockedWallet(page, ethereumWalletMock, {
      privateKey,
      redirectTo: REDIRECT_TO
    })

    // Re-navigate to the deeplink-specific login URL — the same URL the
    // Explorer opens. Re-bind the wallet mock on the new page.
    const deeplinkPath = buildDeeplinkLoginPath(authRequestId)
    await page.goto(deeplinkPath, { waitUntil: 'load' })
    await ethereumWalletMock.connectToDapp()
    await ethereumWalletMock.importWalletFromPrivateKey(privateKey)
    await applyPersonalSignOverride(page)

    // Trigger the wallet sign-in. The `loginMethod=metamask` query param may
    // auto-select MetaMask; click explicitly via the AuthPage POM in case the
    // dapp still shows the method selector.
    await new AuthPage(page).clickMetaMaskButton()

    // Complete the new-user quick-setup flow.
    const qs = new QuickSetupPage(page)
    await qs.waitFor()
    await qs.fillUsername(uniqueUsername())
    await qs.acceptTerms()
    await qs.submit()
    await qs.clickStartExploring()

    // Wait for the auth dapp to redirect via deep link.
    const deepLinkUrl = await waitForDeepLinkRedirect(page, 120_000)
    const params = parseDeepLinkUrl(deepLinkUrl)

    // The deep link must echo the authRequestId so the Explorer can match it
    // to the login that opened this browser flow.
    expect(params.authRequestId).toBe(authRequestId)

    // The signin param carries the identity ID the Explorer will fetch.
    expect(params.signin).toBeTruthy()

    // Verify the identity is retrievable from the auth server — the same
    // call the Explorer's DappDeepLinkAuthenticator makes.
    const identity = await fetchIdentity(params.signin!)
    expect(identity.authChain).toBeDefined()
    expect(identity.authChain.length).toBeGreaterThanOrEqual(2)

    // The SIGNER link in the auth chain must be the wallet address that signed.
    const signerLink = identity.authChain.find(link => link.type === 'SIGNER')
    expect(signerLink).toBeDefined()
    expect(signerLink!.payload.toLowerCase()).toBe(account.address.toLowerCase())

    await unmockProfile()
  })

  test('recurrent user can sign in via deeplink flow (skips quick-setup)', async ({ page, ethereumWalletMock }) => {
    const privateKey = generatePrivateKey()
    const account = privateKeyToAccount(privateKey)

    // ── Phase 1: register a profile for this wallet via the normal flow ──
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

    // ── Phase 2: re-login via the deeplink flow ──
    const authRequestId = generateAuthRequestId()

    // Install the deep link capture for the upcoming navigation.
    await installDeeplinkCapture(page)

    // Navigate to the deeplink login URL with the same (now-registered) wallet.
    const deeplinkPath = buildDeeplinkLoginPath(authRequestId)
    await page.goto(deeplinkPath, { waitUntil: 'load' })
    await ethereumWalletMock.connectToDapp()
    await ethereumWalletMock.importWalletFromPrivateKey(privateKey)
    await applyPersonalSignOverride(page)

    // Click MetaMask in case the method selector is shown.
    await new AuthPage(page).clickMetaMaskButton()

    // The recurrent user should NOT see quick-setup — the auth dapp should
    // create an identity and redirect via deep link directly.
    const deepLinkUrl = await waitForDeepLinkRedirect(page, 120_000)
    const params = parseDeepLinkUrl(deepLinkUrl)

    expect(params.authRequestId).toBe(authRequestId)
    expect(params.signin).toBeTruthy()

    // Verify the identity is valid and belongs to our wallet.
    const identity = await fetchIdentity(params.signin!)
    expect(identity.authChain).toBeDefined()
    expect(identity.authChain.length).toBeGreaterThanOrEqual(2)

    const signerLink = identity.authChain.find(link => link.type === 'SIGNER')
    expect(signerLink).toBeDefined()
    expect(signerLink!.payload.toLowerCase()).toBe(account.address.toLowerCase())

    // Confirm quick-setup was never shown (the URL should not have visited it).
    expect(page.url()).not.toMatch(/\/auth\/quick-setup/)
  })

  test('deeplink login identity contains valid ephemeral key and expiration', async ({ page, ethereumWalletMock }) => {
    const privateKey = generatePrivateKey()
    const authRequestId = generateAuthRequestId()

    // Force the new-user path so we control the flow deterministically.
    await installDeeplinkCapture(page)
    const unmockProfile = await mockNoProfileOnCatalysts(page)
    await setupMockedWallet(page, ethereumWalletMock, { privateKey, redirectTo: REDIRECT_TO })

    const deeplinkPath = buildDeeplinkLoginPath(authRequestId)
    await page.goto(deeplinkPath, { waitUntil: 'load' })
    await ethereumWalletMock.connectToDapp()
    await ethereumWalletMock.importWalletFromPrivateKey(privateKey)
    await applyPersonalSignOverride(page)

    await new AuthPage(page).clickMetaMaskButton()

    // Complete quick-setup (new-user flow, guaranteed by the profile mock).
    const qs = new QuickSetupPage(page)
    await qs.waitFor()
    await qs.fillUsername(uniqueUsername())
    await qs.acceptTerms()
    await qs.submit()
    await qs.clickStartExploring()

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

    await unmockProfile()
  })
})
