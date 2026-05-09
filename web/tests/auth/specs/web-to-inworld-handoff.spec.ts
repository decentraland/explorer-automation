import { test, expect } from '@playwright/test'
import { LandingPage } from '../pages/LandingPage.js'
import { AuthPage } from '../pages/AuthPage.js'
import { HomePage } from '../pages/HomePage.js'
import { generateFreshEmail, waitForOtp } from '../helpers/otp-mailbox.js'
import { removeTokenBridge, waitForTokenBridge } from '../helpers/token-bridge.js'
import { runExplorer, verifyExplorerInWorld } from '../helpers/explorer-runner.js'
import type { ChildProcess } from 'node:child_process'

// TODO: the homepage CTA that writes auth-token-bridge.txt has not yet been
// confirmed against the live dapp (the visible "DOWNLOAD FOR macOS" CTA only
// downloads the launcher; the actual handoff button needs codegen recording).
// Skipped until that step is captured. Once known, fill in the gap below.
test.describe.skip('@cross web → desktop handoff', () => {
  let explorer: ChildProcess | undefined

  test.beforeEach(async () => {
    await removeTokenBridge()
  })

  test.afterEach(async () => {
    if (explorer && !explorer.killed) {
      explorer.kill('SIGTERM')
    }
  })

  test('web login writes token bridge and Explorer lands in-world', async ({ page }) => {
    const email = generateFreshEmail()

    const landing = new LandingPage(page)
    const auth = new AuthPage(page)
    const home = new HomePage(page)

    await landing.goto()
    await landing.clickSignIn()
    await auth.submitEmail(email)
    await auth.waitForOtpScreen()
    const code = await waitForOtp(email)
    await auth.enterOtp(code)
    await home.waitFor(120_000)

    // TODO: trigger the "Jump Into Decentraland" CTA that writes the bridge file.
    // Selector unknown until codegen records that flow.

    const bridgeContents = await waitForTokenBridge(30_000)
    expect(bridgeContents.trim().length).toBeGreaterThan(0)

    explorer = runExplorer({ alttester: true })
    await verifyExplorerInWorld()
  })
})
