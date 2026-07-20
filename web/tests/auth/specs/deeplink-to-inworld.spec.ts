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
  parseDeepLinkUrl
} from '../helpers/deeplink.js'
import { writeDeeplinkBridge, removeDeeplinkBridge } from '../helpers/deeplink-bridge.js'
import { runExplorer, verifyExplorerInWorldFromDeeplink } from '../helpers/explorer-runner.js'
import type { ChildProcess } from 'node:child_process'

const { expect } = test
const REDIRECT_TO = `${getBaseUrl()}/`

test.describe('@cross deeplink → desktop handoff', () => {
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
    await page.goto(buildDeeplinkLoginPath(authRequestId), { waitUntil: 'load' })
    await applyPersonalSignOverride(page)

    const deepLinkUrl = await waitForDeepLinkRedirect(page, 120_000)
    const params = parseDeepLinkUrl(deepLinkUrl)
    expect(params.signin).toBeTruthy()
    expect(params.authRequestId).toBe(authRequestId)

    // ── Write deeplink-bridge.json (same format the launcher writes) ──
    // Format: {"deeplink": "decentraland://open?signin={identityId}&authRequestId={uuid}"}
    // DeeplinkSentinel polls for this file and feeds it to DeepLinkHandle.
    await writeDeeplinkBridge(deepLinkUrl)

    // ── Launch Explorer and verify in-world ──
    explorer = runExplorer({ alttester: true })
    await verifyExplorerInWorldFromDeeplink()
  })
})
