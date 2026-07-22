import { generatePrivateKey, privateKeyToAccount } from 'viem/accounts'
import { uniqueUsername } from '../helpers/test-user.js'
import { walletTest as test } from '../../../shared/fixtures/wallet-fixture.js'
import {
  setupMockedWallet,
  mockNoProfileOnCatalysts,
  installAutoWalletMockInitScript,
  installPersonalSignOverrideInitScript
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
  parseDeepLinkUrl
} from '../helpers/deeplink.js'
import { writeDeeplinkBridge, removeDeeplinkBridge, deeplinkBridgeExists } from '../helpers/deeplink-bridge.js'
import { runExplorer, verifyExplorerInWorldFromDeeplink } from '../helpers/explorer-runner.js'
import type { ChildProcess } from 'node:child_process'

const { expect } = test
const REDIRECT_TO = `${getBaseUrl()}/`

test.describe('@cross deeplink → desktop handoff', () => {
  // P1 fix: the cross flow covers profile creation (~110s), deeplink wait (up to 120s),
  // Explorer launch, and dotnet/AltTester verification (several minutes). The global
  // 120s timeout cannot cover this — size for the full chain.
  test.describe.configure({ timeout: 600_000 })

  let explorer: ChildProcess | undefined

  test.beforeEach(async () => {
    await removeDeeplinkBridge()
  })

  test.afterEach(async () => {
    await removeDeeplinkBridge()
    if (explorer && !explorer.killed) {
      explorer.kill('SIGTERM')
    }
  })

  test('deeplink auth writes bridge and Explorer lands in-world', async ({ page, ethereumWalletMock }) => {
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
    await installDeeplinkCapture(page)
    await installAutoWalletMockInitScript(page, account.address)
    await installPersonalSignOverrideInitScript(page)
    await page.goto(buildDeeplinkLoginPath(authRequestId), { waitUntil: 'load' })

    const deepLinkUrl = await waitForDeepLinkRedirect(page, 120_000)
    const params = parseDeepLinkUrl(deepLinkUrl)
    expect(params.signin).toBeTruthy()
    expect(params.authRequestId).toBe(authRequestId)

    // ── Write deeplink-bridge.json (same format the launcher writes) ──
    // Format: {"deeplink": "decentraland://open?signin={identityId}&authRequestId={uuid}"}
    // DeeplinkSentinel polls for this file and feeds it to DeepLinkHandle.
    await writeDeeplinkBridge(deepLinkUrl)

    // ── Launch Explorer and verify in-world ──
    // Use `clear: true` to wipe the launcher's Thirdweb auth cache, ensuring the
    // test can only pass if the bridge file is consumed (not stale cached auth).
    explorer = runExplorer({ alttester: true, clear: true })
    await verifyExplorerInWorldFromDeeplink()

    // Verify the bridge file was consumed (deleted) by DeeplinkSentinel — proves
    // the Explorer actually read and processed the deeplink, not stale auth state.
    const bridgeStillExists = await deeplinkBridgeExists()
    expect(bridgeStillExists).toBe(false)
  })
})
