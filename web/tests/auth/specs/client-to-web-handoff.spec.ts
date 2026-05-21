import { generatePrivateKey, privateKeyToAccount } from 'viem/accounts'
import { randomBytes } from 'node:crypto'
import { walletTest as test } from '../../../shared/fixtures/wallet-fixture.js'
import {
  setupMockedWallet,
  mockNoProfileOnCatalysts,
  installAutoWalletMockInitScript,
  applyPersonalSignOverride
} from '../helpers/wallet.js'
import { AuthPage } from '../pages/AuthPage.js'
import { QuickSetupPage } from '../pages/QuickSetupPage.js'
import {
  readUnityVerificationCode,
  removeAuthHandoff,
  removeUnityVerificationCode,
  waitForAuthHandoff
} from '../helpers/auth-request-bridge.js'
import { runExplorerTest, setupExplorerStack, teardownExplorerStack } from '../helpers/explorer-runner.js'

/**
 * Flow 1 — client-first wallet device pairing. **Orchestrator only.** Unity
 * assertions live in C# `[Test]` fixtures dispatched via `runExplorerTest`;
 * web assertions live in `helpers/wallet.ts` and the page objects invoked
 * here. The body below is pure sequencing.
 *
 * Orchestration phases:
 *
 *   1. **Dispatch the Unity click in parallel with web pre-warm.** C#
 *      `TestCaptureWalletAuthHandoff` clicks Metamask in the Explorer auth
 *      screen — the client's auth-api round trip ends with
 *      `Application.OpenURL`, which the ALTTESTER-gated hook in
 *      `UnityAppWebBrowser` intercepts to write `auth-url.txt`. C# asserts
 *      the file appears and exits. In parallel, Playwright signs up a fresh
 *      DCL profile on `/auth/login` so the wallet identity exists by the
 *      time we reach the requests page.
 *   2. **Read the handoff signal.** `auth-url.txt` appearing IS the signal
 *      that Unity has fired its auth request. We parse the URL to extract
 *      `requestId` from the `redirectTo` query param.
 *   3. **Navigate to the requests page.** Playwright navigates its Chromium
 *      to `/auth/requests/<requestId>?targetConfigId=default`. Auth-api sees
 *      the connection and emits the `request-validation-status` SocketIO
 *      event the Unity client has been waiting for — Unity's verification
 *      screen activates at the same time. Both sides now show the same
 *      validation code.
 *   4. **Approve on the web.** The signature flows back over the auth-api
 *      websocket to the client; the client transitions in-world.
 *   5. **Dispatch the in-world C# stage.** `TestInWorldAndRunEmote` asserts
 *      the HUD is up and plays Fist Pump.
 *
 * Stays `test.describe.skip` until two prereqs land:
 *   - unity-explorer branch `chore/expose-requestid-for-cross-tests` (carries
 *     the OpenURL hook) is merged or pinned via `EXPLORER_BUILD_TARGET`.
 *   - RequestPage code-element selector confirmed (currently a TODO; the
 *     test currently doesn't require it because successful auth implies
 *     the codes matched — auth-api enforces it server-side).
 *
 * Future enhancement (deferred): explicitly assert the Unity-side code equals
 * the web-side code via a second `runExplorerTest('TestReadVerificationCode')`
 * stage that reads `Verification.Dapp.Screen.Code` after the requests page
 * is loaded (the screen activates then). Tracked as a follow-up.
 */
const uniqueUsername = (): string => `QA${randomBytes(3).toString('hex')}`

const { expect } = test

test.describe('@cross client → web wallet handoff', () => {
  // One-time stack setup: install (if needed) + launch the AltTester-instrumented
  // Explorer build + start AltTester Desktop. Subsequent `runExplorerTest` calls
  // hit the already-running stack via plain `dotnet test`.
  test.beforeAll(async () => {
    await setupExplorerStack()
  })

  test.afterAll(() => {
    teardownExplorerStack()
  })

  test.beforeEach(async () => {
    await removeAuthHandoff()
    await removeUnityVerificationCode()
  })

  test('wallet login via web reaches the requests page and lands client in-world', async ({
    page,
    ethereumWalletMock
  }) => {
    // Phase 1: dispatch Unity click in parallel with web profile signup.
    const unityCapture = runExplorerTest('TestCaptureWalletAuthHandoff')

    // Wallet selection: prefer a pre-registered test wallet from env to avoid
    // creating spam profiles on catalysts every run. Falls back to a fresh
    // wallet + QuickSetup signup if the env var is unset (clean-environment
    // friendly).
    const existingPk = process.env['AUTH_TEST_WALLET_PRIVATE_KEY'] as `0x${string}` | undefined
    const privateKey = existingPk ?? generatePrivateKey()
    const account = privateKeyToAccount(privateKey)

    if (existingPk) {
      // Existing wallet path: dapp finds the profile on catalysts → routes
      // directly to homepage post-sign-in, no QuickSetup involved. We DON'T
      // mock no-profile here.
      await setupMockedWallet(page, ethereumWalletMock, { privateKey, redirectTo: '/' })
      await new AuthPage(page).clickMetaMaskButton()
    } else {
      // Fresh-wallet path: mirror request-page.spec.ts — mock catalysts to
      // return 404 so the dapp routes through /auth/quick-setup, then walk
      // through the signup form to register a profile for the new wallet.
      const unmockProfile = await mockNoProfileOnCatalysts(page)
      await setupMockedWallet(page, ethereumWalletMock, { privateKey, redirectTo: '/' })
      await new AuthPage(page).clickMetaMaskButton()
      const qs = new QuickSetupPage(page)
      await qs.waitFor()
      await qs.fillUsername(uniqueUsername())
      await qs.acceptTerms()
      await qs.submit()
      await qs.clickStartExploring()
      await unmockProfile()
    }

    // Phase 2: wait for the Unity click + URL-file write to complete, then
    // read the captured URL (and extracted requestId) from disk.
    await unityCapture
    const handoff = await waitForAuthHandoff()

    expect(handoff.requestId).toMatch(/^[A-Za-z0-9_-]+$/)
    expect(handoff.url).toContain(`/auth/requests/${handoff.requestId}`)

    // Phase 3: navigate to the requests page. Once the page connects to
    // auth-api, the server emits `request-validation-status` to the Unity
    // client → Verification.Dapp.Screen appears with the code. Both sides
    // are now showing the same validation code.
    await installAutoWalletMockInitScript(page, account.address)
    await page.goto(`/auth/requests/${handoff.requestId}?targetConfigId=default`, { waitUntil: 'load' })
    await applyPersonalSignOverride(page)

    // Phase 4: device-pairing safety check. This is the ACTUAL product
    // feature — code on client MUST equal code on web. Skipping this
    // (clicking Approve immediately) makes the test green but doesn't
    // validate the security property real users rely on.
    //
    // Wait for the "Verify Sign In" screen to be ready by waiting for the
    // approve button (well-known testid carried over from request-page.spec.ts).
    // That doubles as the "code is rendered" signal — the dapp shows code +
    // buttons together.
    const approveBtn = page.locator('[data-testid="verify-sign-in-approve-button"]')
    await approveBtn.waitFor({ state: 'visible', timeout: 60_000 })

    // Read the web-side code. The dapp renders it as separate per-digit
    // elements (`<div class="MuiBox-root …">1</div><div …>3</div>`) grouped
    // under a `_left_*` parent. innerText doesn't capture this due to CSS
    // positioning, so we deep-scan the DOM for digit-only leaves and
    // concatenate per-parent.
    // TODO(request-page-code-selector): tighten to data-testid once known.
    const readWebCode = (): Promise<string | null> =>
      page.evaluate(() => {
        // Any leaf element whose trimmed text is digit-only. Catches both
        // single-element codes (`<div>78</div>`) and per-digit codes
        // (`<div>1</div><div>3</div>`) — the latter we still need to
        // concatenate per parent.
        const groups = new Map<Element, string[]>()
        document.querySelectorAll('*').forEach(el => {
          const t = (el.textContent ?? '').trim()
          if (/^\d+$/.test(t) && el.children.length === 0 && el.parentElement) {
            const arr = groups.get(el.parentElement) ?? []
            arr.push(t)
            groups.set(el.parentElement, arr)
          }
        })
        const grouped = Array.from(groups.values()).map(d => d.join(''))
        return grouped.find(s => s.length >= 1) ?? null
      })

    const webCodeDeadline = Date.now() + 30_000
    let webCode: string | null = null
    while (Date.now() < webCodeDeadline) {
      webCode = await readWebCode()
      if (webCode) break
      await page.waitForTimeout(500)
    }
    if (!webCode) {
      const bodyText = (await page.locator('body').innerText()).slice(0, 1500)
      throw new Error(`Web verification code didn't render in 30s. innerText:\n${bodyText}`)
    }
    expect(webCode).toMatch(/^\d+$/)

    // Read the Unity-side code via the C# stage. The Verification.Dapp.Screen
    // is now visible on the client (the auth-api request-validation event
    // fired when our browser connected above).
    await runExplorerTest('TestReadVerificationCode')
    const unityCode = await readUnityVerificationCode()
    expect(unityCode).toMatch(/^\d+$/)

    expect(webCode, 'device-pairing code on web must match code on Unity client').toBe(unityCode)

    // Phase 5: approve on the web ("YES, THEY ARE THE SAME"). Signature flows
    // over auth-api websocket to the Unity client; the client transitions
    // in-world. C# verifies + emote.
    await approveBtn.click()

    await runExplorerTest('TestInWorldAndRunEmote')
  })
})
