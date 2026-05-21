import { test, expect } from '@playwright/test'
import type { ChildProcess } from 'node:child_process'
import { LandingPage } from '../../landing/pages/LandingPage.js'
import { AuthPage } from '../pages/AuthPage.js'
import { generateFreshEmail, waitForOtp } from '../helpers/otp-mailbox.js'
import { removeTokenBridge, waitForTokenBridge } from '../helpers/token-bridge.js'
import { runExplorer, runExplorerTest } from '../helpers/explorer-runner.js'

/**
 * Flow 2 — web-first: user logs in via OTP on the landing site, downloads the
 * launcher, and the landing page writes `auth-token-bridge.txt`. Then the
 * Explorer is launched and the launcher's `TokenFileAuthenticator` picks up
 * the bridge file, drops the player straight in-world, and the C# fixture
 * verifies and runs an emote.
 *
 * Currently `test.describe.skip` until the post-login "Jump Into Decentraland"
 * CTA selector on the landing page is identified. To unblock:
 *
 *   1. Log in manually at <WEB_BASE_URL>/auth with a fresh OTP email.
 *   2. Watch ~/Library/Application Support/DecentralandLauncherLight/
 *      auth-token-bridge.txt while clicking candidate CTAs (e.g. `fswatch` on
 *      that path while in another terminal).
 *   3. Once you've found the CTA that writes the file, replace the TODO below
 *      with the `getByRole`/`getByText` call.
 *   4. Remove `test.describe.skip` and run the spec via
 *      `npx playwright test --project=cross --grep '@cross web → client'`.
 */
test.describe.skip('@cross web → client handoff', () => {
  let explorer: ChildProcess | undefined

  test.beforeEach(async () => {
    await removeTokenBridge()
  })

  test.afterEach(() => {
    if (explorer && !explorer.killed) {
      explorer.kill('SIGTERM')
    }
  })

  test('web OTP login writes token bridge, Explorer lands in-world and plays emote', async ({ page }) => {
    const email = generateFreshEmail()

    const landing = new LandingPage(page)
    const auth = new AuthPage(page)

    // 1. OTP login.
    await landing.goto()
    await landing.clickSignIn()
    await auth.submitEmail(email)
    await auth.waitForOtpScreen()
    await auth.enterOtp(await waitForOtp(email))
    await landing.waitForUrl(120_000)

    // 2. Exercise the public download CTA. We don't install the artifact — the
    //    actual Explorer the test drives comes from metaforge — but completing
    //    the download flow catches regressions in the link that the user-facing
    //    "Get Started" path returns.
    const download = await landing.downloadLauncher()
    expect((await download.suggestedFilename()).length).toBeGreaterThan(0)

    // 3. Trigger the "Jump Into Decentraland" handoff CTA. Writes
    //    auth-token-bridge.txt to the launcher's local Application Support
    //    directory.
    // TODO(handoff-cta): selector still unknown — fill in once codegen
    // recording identifies the post-login button that writes the bridge file.
    throw new Error('Jump Into Decentraland selector not yet identified — see TODO above')

    // 4. Wait for the bridge file, then launch Explorer.
    const bridgeContents = await waitForTokenBridge(30_000)
    expect(bridgeContents.trim().length).toBeGreaterThan(0)

    explorer = runExplorer({ alttester: true })
    await runExplorerTest('TestInWorldAndRunEmote')
  })
})
