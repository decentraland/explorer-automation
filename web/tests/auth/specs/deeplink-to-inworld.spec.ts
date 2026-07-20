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
import { removeTokenBridge, writeTokenBridge } from '../helpers/token-bridge.js'
import { runExplorer, verifyExplorerInWorldFromDeeplink } from '../helpers/explorer-runner.js'
import type { ChildProcess } from 'node:child_process'

const { expect } = test
const REDIRECT_TO = `${getBaseUrl()}/`

test.describe('@cross deeplink → desktop handoff', () => {
  let explorer: ChildProcess | undefined

  test.beforeEach(async () => {
    await removeTokenBridge()
  })

  test.afterEach(async () => {
    if (explorer && !explorer.killed) {
      explorer.kill('SIGTERM')
    }
  })

  test('deeplink auth writes token bridge and Explorer lands in-world', async ({ page, ethereumWalletMock }) => {
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

    // ── Write identity ID to token bridge ──
    // TokenFileAuthenticator reads a GUID from auth-token-bridge.txt, fetches
    // GET /identities/{guid}, and auto-logs in. The deeplink flow's signin
    // param IS that identity ID.
    await writeTokenBridge(params.signin!)

    // ── Launch Explorer and verify in-world ──
    explorer = runExplorer({ alttester: true })
    await verifyExplorerInWorldFromDeeplink()
  })
})
