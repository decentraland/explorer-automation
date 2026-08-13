import { test, expect } from '../../../shared/fixtures/base-test.js'
import { LandingPage } from '../../landing/pages/LandingPage.js'
import { AuthPage } from '../pages/AuthPage.js'
import { QuickSetupPage } from '../pages/QuickSetupPage.js'
import { PostSignupPage } from '../pages/PostSignupPage.js'
import { generateFreshEmail, waitForOtp } from '../helpers/otp-mailbox.js'
import {
  removeExpectedUsername,
  removeTokenBridge,
  writeExpectedUsername,
  writeTokenBridge
} from '../helpers/token-bridge.js'
import { parseAuthTokenFromDownloadUrl } from '../helpers/download-gateway.js'
import { uniqueUsername } from '../helpers/test-user.js'
import { getBaseUrl } from '../../../shared/helpers/env.js'
import {
  clearExplorerIdentityCache,
  launchInstrumentedExplorer,
  runExplorerTest,
  startAltTesterDesktop,
  teardownExplorerStack
} from '../helpers/explorer-runner.js'

/**
 * Flow 2 — web-first: fresh-user OTP signup on the dapp produces a
 * personalised download CTA whose URL embeds an auth-redeemable session UUID.
 * The orchestrator captures the URL, derives the UUID (the launcher would do
 * the same from the .dmg's `kMDItemWhereFroms` xattr — see
 * `helpers/download-gateway.ts` for the verbatim algorithm port from
 * `launcher-rust core/src/auto_auth.rs`), writes it to `auth-token-bridge.txt`,
 * and asserts the instrumented Explorer boots authenticated **as the right
 * user** + runs an emote.
 *
 * The identity assertion (`TestInWorldUsernameMatches`) is load-bearing: a
 * raw "Explorer reached in-world" can pass on a launcher that silently
 * consumed a stale bridge file or reused a profile cache from a prior run.
 * Comparing the in-world username to the QuickSetup username is the only
 * cheap way to catch that class of cache-pollution bugs.
 *
 * Both locators that previously blocked this spec are resolved:
 *   - Post-signup CTA: `a.hero-download-btn` (matched via
 *     `getByRole('link', { name: /^DOWNLOAD FOR/i }).first()` per the
 *     auth-surface locator priority).
 *   - In-world username: `//Lobby.ExistingAccount.Screen//Title` (the
 *     "Welcome back <username>" greeting on the cached-account sub-screen
 *     of `Authentication.MainScreen(Clone)`).
 */

// The end-to-end flow exceeds the global 120s test timeout (line 51 of
// playwright.config.ts): OTP signup ~30s + QuickSetup ~10s + CTA capture ~5s +
// bridge write ~1s + Explorer first-launch ~30s + C# welcome-screen poll up
// to 180s for cold catalyst lookup on a brand-new account = ~4 min. Bump
// here rather than on the project, so the longer budget is local to the
// cross-handoff describe and the project default still catches genuine
// hangs in other @cross specs.
test.describe.configure({ timeout: 480_000 })

test.describe('@cross web → client handoff', () => {
  test.beforeAll(async () => {
    // Spin up AltTester Desktop only; the Explorer launch is deferred to
    // the test body — Flow 2's production sequence is "web sign-in → click
    // download → THEN open the (never-yet-running) client", because the
    // client's first-launch hook is what reads `auth-token-bridge.txt`.
    await startAltTesterDesktop()
  })

  test.afterAll(() => {
    teardownExplorerStack()
  })

  test.beforeEach(async () => {
    await removeTokenBridge()
    await removeExpectedUsername()
    // Wipe any prior Thirdweb identity cache so the bridge token isn't
    // shadowed by a stale logged-in account when the client boots.
    clearExplorerIdentityCache()
  })

  test('fresh-user signup boots the client authenticated as the signed-up identity', async ({ page }) => {
    const email = generateFreshEmail()
    const generatedUsername = uniqueUsername()
    const landing = new LandingPage(page)
    const auth = new AuthPage(page)
    const setup = new QuickSetupPage(page)
    const postSignup = new PostSignupPage(page)

    // 1. OTP. Two paths after the OTP is entered:
    //   - **New-user** (`TEST_EMAIL_PREFIX=qa-` + a Workspace catch-all
    //     domain): every signup is a brand-new account. URL hits
    //     `/auth/quick-setup`; we fill the username + accept terms.
    //   - **Recurrent** (`TEST_EMAIL_PREFIX=<user>+qa-`): the dapp's
    //     identity layer normalises `+alias` to the base account, so the
    //     "signup" is actually a recurrent login. URL skips straight to
    //     `/`. The expected username for the assertion is the existing
    //     account's display name — supplied via `AUTH_TEST_EXPECTED_USERNAME`.
    //
    // Race the two URLs to detect which path we're on, then resolve the
    // username we'll feed to the C# identity guard.
    await landing.goto()
    await landing.clickSignIn()
    await auth.submitEmail(email)
    await auth.waitForOtpScreen()
    await auth.enterOtp(await waitForOtp(email))

    // Detect which path the post-OTP navigation takes by racing two URL
    // observers — but only the DETECTION races, not the path walks. A
    // recurrent race that bundled the per-path action would propagate its
    // own URL timeout through Promise.race and abort the test while
    // QuickSetup was still being filled in.
    const quickSetupRe = /\/auth\/quick-setup/
    const landingHost = new URL(getBaseUrl()).host.replace(/\./g, '\\.')
    const landingRe = new RegExp(`^https?://${landingHost}\\/?(\\?.*)?$`)
    const detectNewUser = page
      .waitForURL(quickSetupRe, { timeout: 120_000 })
      .then(() => 'new-user' as const)
      .catch(() => null)
    const detectRecurrent = page
      .waitForURL(url => landingRe.test(url.toString()), { timeout: 120_000 })
      .then(() => 'recurrent' as const)
      .catch(() => null)
    const detected = await Promise.race([detectNewUser, detectRecurrent])

    let username: string
    if (detected === 'new-user') {
      await setup.fillUsername(generatedUsername)
      await setup.acceptTerms()
      await setup.submit()
      await setup.clickStartExploring()
      await landing.waitForUrl(120_000)
      username = generatedUsername
    } else if (detected === 'recurrent') {
      const fromEnv = process.env['AUTH_TEST_EXPECTED_USERNAME']
      if (!fromEnv) {
        throw new Error(
          'Recurrent signup path detected (no /auth/quick-setup) but ' +
            'AUTH_TEST_EXPECTED_USERNAME is unset. Set it to the existing ' +
            "account's display name (the value the welcome screen renders " +
            "after 'Welcome back '), or switch to a TEST_EMAIL_PREFIX/EMAIL_DOMAIN " +
            'pair that produces genuinely new email addresses.'
        )
      }
      username = fromEnv
    } else {
      throw new Error('Neither /auth/quick-setup nor the landing URL was reached within 120s after OTP entry')
    }

    // 2. Trigger the personalised download CTA. The dapp `fetch`-es the
    //    .dmg bytes from download-gateway and re-serves them as a Blob, so
    //    `download.url()` would return a `blob:` URL with no trace of the
    //    gateway URL. The POM intercepts the underlying network request
    //    instead — that's what carries the auth-redeemable UUID.
    const gatewayUrl = await postSignup.captureSignedDownloadUrl()
    expect(gatewayUrl).toMatch(/download-gateway\.decentraland\.(org|zone|today)/)

    // 3. Mirror the launcher's auto_auth UUID-pick algorithm in pure JS.
    //    No HTTP exchange — the UUID IS the auth token.
    const token = parseAuthTokenFromDownloadUrl(gatewayUrl)
    expect(token, 'parsed auth-token must be a non-empty UUID').toMatch(/^[0-9a-f-]+$/i)

    // 4. Hand off to the launcher data dir; tell the C# fixture which
    //    identity to assert. Both files land in
    //    ~/Library/Application Support/DecentralandLauncherLight/.
    await writeTokenBridge(token)
    await writeExpectedUsername(username)

    // 5. First-time client launch. The Explorer's TokenFileAuthenticator
    //    reads `auth-token-bridge.txt` on startup, authenticates, and
    //    lands the user on the cached-account "Welcome back <username>"
    //    sub-screen of Authentication.MainScreen. This is the production
    //    handoff sequence — anything that pre-launches the Explorer
    //    invalidates Flow 2's contract.
    await launchInstrumentedExplorer()

    //    Stage 5a: identity assert — reads the welcome screen's label,
    //    compares to the QuickSetup username. Catches stale-bridge or
    //    profile-cache regressions that would otherwise pass 5b silently.
    //    Stage 5b: click Jump Into Decentraland + in-world + emote
    //    (BaseTest.EnsureInWorld handles the auth-screen → in-world ride).
    await runExplorerTest('TestInWorldUsernameMatches')
    await runExplorerTest('TestInWorldAndRunEmote')
  })
})
