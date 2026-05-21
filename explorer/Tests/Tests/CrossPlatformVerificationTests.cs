namespace ExplorerAutomation.Tests.Tests;

/// <summary>
/// Path the Unity client writes the opened auth URL to, via the OpenURL hook
/// in `UnityAppWebBrowser.cs` (compile-time-gated under `#if ALTTESTER`).
/// Mirrors `getAuthUrlPath()` in `web/tests/auth/helpers/auth-request-bridge.ts`.
/// Lives next to `auth-token-bridge.txt` so launcher-local handshake state for
/// both directions of the cross-stack handoff stays in one directory.
///
/// File contents: the raw URL the client opened, e.g.
/// `https://decentraland.org/auth/login?redirectTo=%2Fauth%2Frequests%2F&lt;id&gt;%3FtargetConfigId%3Ddefault`.
/// The Playwright spec parses out the requestId itself from the redirectTo
/// query param.
/// </summary>
internal static class CrossPlatformPaths
{
    public static string AuthUrlPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Library", "Application Support", "DecentralandLauncherLight",
            "auth-url.txt");

    /// <summary>
    /// Path the C# stage writes the Unity-side validation code to once the
    /// Verification.Dapp.Screen activates. Playwright reads this and asserts
    /// it equals the code rendered on the web RequestPage — the actual
    /// device-pairing safety check end-users perform visually.
    /// </summary>
    public static string AuthVerificationCodePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Library", "Application Support", "DecentralandLauncherLight",
            "auth-verification-code.txt");
}

/// <summary>
/// Convergence fixture for both `@cross` flows. Shelled out by the Playwright
/// specs via `runExplorerTest('TestInWorldAndRunEmote')` once the Explorer is
/// already in-world (Flow 2: token-bridge auto-login; Flow 1: post-wallet-sign
/// transition). Asserts the player-facing HUD is up and exercises a Fist Pump
/// emote — catches regressions in both the auth handshake and the post-login
/// in-world experience.
/// </summary>
[AllureSuite("Cross-Platform Verification")]
[Category("CrossVerify")]
[Order(15)]
public class CrossPlatformVerificationTests : BaseTest
{
    [Test]
    public void TestInWorldAndRunEmote()
    {
        Assert.That(Views.MainMenu.IsPresent(), Is.True,
            "Main menu (sidebar) should be visible after the auth handoff completes.");

        // Equip Fist Pump to slot 0 — mirrors BackpackEmotesTests.TestSearchAndEquipFistPump
        // verbatim, then triggers the equipped emote with the slot-0 hotkey (`1`).
        PressKey(AltKeyCode.I);
        Views.ExplorePanel.WaitFor();
        Views.ExplorePanel.Backpack.EmotesTabButton.Click();

        Views.ExplorePanel.Backpack.SearchBar.SetText("Fist Pump");
        Wait(2);

        Views.ExplorePanel.Backpack.Emotes.UnequipEmoteIfPresent(0);
        Views.ExplorePanel.Backpack.Emotes.SetEmote(0, 0);
        Reporter.Log("Fist Pump equipped to slot 0");

        Views.ExplorePanel.Close();
        Views.ExplorePanel.WaitForGone();

        // Trigger slot 0. The avatar animation is not observable via AltTester reliably,
        // so the assertion is implicit: the hotkey doesn't throw, the backpack stays
        // closed, the client stays connected.
        PressKey(AltKeyCode.Alpha1);
        Wait(3);
        Reporter.Log("Slot 0 emote triggered");
    }
}

/// <summary>
/// Flow 1 stage 1 — fire the wallet auth flow and confirm the client opened
/// the system-browser URL. Runs against a freshly-launched (logged-out)
/// Explorer. Extends <see cref="LoggedOutAuthBaseTest"/> so its
/// `EnsureInWorld` override lands the client at the LoginSelection screen
/// regardless of starting state.
///
/// Clicks the Metamask button. The client opens a SocketIO websocket to
/// auth-api, posts a `dcl_personal_sign` request, receives back
/// `{ requestId, code }`, and calls `webBrowser.OpenUrl(...)` to launch the
/// system browser. The ALTTESTER-gated hook in `UnityAppWebBrowser.OpenUrl`
/// (see the matching unity-explorer commit) writes the opened URL to
/// <see cref="CrossPlatformPaths.AuthUrlPath"/> at that moment. This fixture
/// asserts the file appears within a generous timeout — the Playwright spec
/// then reads the file, derives the requestId, and drives its own browser to
/// the same `/auth/requests/<id>` URL.
///
/// We intentionally do NOT wait for `Verification.Dapp.Screen` here: that
/// screen only appears AFTER the server emits a `request-validation-status`
/// SocketIO event, which only happens once the browser side reaches the
/// requests page. Waiting on it would deadlock with the Playwright orchestrator
/// (which is itself waiting for this stage to complete).
///
/// Lives in its own fixture (separate from <see cref="CrossPlatformVerificationTests"/>)
/// because it requires a *logged-out* `OneTimeSetUp`, whereas the convergence
/// fixture needs an *in-world* one.
/// </summary>
[AllureSuite("Cross-Platform Verification")]
[Category("CrossVerify")]
[Order(16)]
public class CrossPlatformWalletHandoffCaptureTests : LoggedOutAuthBaseTest
{
    [Test]
    public void TestCaptureWalletAuthHandoff()
    {
        // Clean any stale URL file from a prior run.
        if (File.Exists(CrossPlatformPaths.AuthUrlPath))
            File.Delete(CrossPlatformPaths.AuthUrlPath);

        Assert.That(Views.AuthenticationMainScreen.LoginSelectionScreen.IsPresent(), Is.True,
            "Expected the LoginSelection sub-screen after LoggedOutAuthBaseTest.EnsureInWorld.");

        Views.AuthenticationMainScreen.MetamaskButton.Click();
        Reporter.Log("Metamask button clicked — waiting for auth-url.txt to appear");

        // Poll for the URL file. The UnityAppWebBrowser.OpenUrl hook writes
        // it the moment Application.OpenURL is called — typically 1-3 seconds
        // after the click (auth-api websocket connect + dcl_personal_sign
        // round trip). Generous 30s timeout for slow CI / cold caches.
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline && !File.Exists(CrossPlatformPaths.AuthUrlPath))
        {
            Thread.Sleep(250);
        }

        Assert.That(File.Exists(CrossPlatformPaths.AuthUrlPath), Is.True,
            $"Expected the OpenURL hook to write {CrossPlatformPaths.AuthUrlPath} within 30s. " +
            "Either the click didn't trigger the auth flow, the auth-api request failed, " +
            "or the running Explorer build doesn't have the ALTTESTER-gated OpenURL hook " +
            "(needs unity-explorer branch `chore/expose-requestid-for-cross-tests` or later).");

        var url = File.ReadAllText(CrossPlatformPaths.AuthUrlPath).Trim();
        Assert.That(url, Does.StartWith("http"),
            $"auth-url.txt should hold an http(s) URL, got: {url}");
        Reporter.Log($"Auth URL captured (Playwright will read this from disk): {url}");
    }
}

/// <summary>
/// Flow 1 stage 2 — read the validation code from Unity's
/// `Verification.Dapp.Screen` once the auth-api has emitted the
/// `request-validation-status` event (i.e. once the browser side has reached
/// `/auth/requests/<id>`). Writes the code to
/// <see cref="CrossPlatformPaths.AuthVerificationCodePath"/> for Playwright to
/// read and compare against the code rendered on the web RequestPage.
///
/// This stage exists *because* the device-pairing safety check is the actual
/// product feature being tested: client and web display the same code, the
/// user verifies they match before approving. Skipping it (clicking Approve
/// immediately) makes the test green but doesn't validate the security
/// property real users rely on.
///
/// Extends <see cref="LoggedOutAuthBaseTest"/> so its `EnsureInWorld` override
/// stays gentle — it sees we're on the auth screen and returns. The
/// verification screen should already be active when this runs.
/// </summary>
[AllureSuite("Cross-Platform Verification")]
[Category("CrossVerify")]
[Order(16)]
public class CrossPlatformVerificationCodeReadTests : LoggedOutAuthBaseTest
{
    /// <summary>
    /// EnsureInWorld override: when the dapp verification screen is active
    /// (post-Metamask, post-browser-reach), neither the LoginSelection nor
    /// the OtpVerification sub-screens are visible, and the cached-account
    /// "Use a Different Account" button is missing — so the parent class's
    /// fallback would throw. We just return without navigation: the test
    /// expects the verification screen to be already present, and any
    /// state-recovery would defeat the purpose of reading the code on it.
    /// </summary>
    protected override void EnsureInWorld()
    {
        // No-op: the orchestrator (Playwright spec) ensures the verification
        // screen is up before invoking this stage.
    }

    [Test]
    public void TestReadVerificationCode()
    {
        // Clean any stale file before writing.
        if (File.Exists(CrossPlatformPaths.AuthVerificationCodePath))
            File.Delete(CrossPlatformPaths.AuthVerificationCodePath);

        // Wait for the verification screen to surface its code. The screen
        // appears once auth-api sends "request-validation-status" — which
        // requires the browser side to be on /auth/requests/<id>. 60s timeout
        // covers the browser navigation + auth-api round-trip.
        var code = Views.VerificationDappAuthScreen.CodeText.GetText(60);
        Assert.That(code, Is.Not.Empty.And.Matches("^[0-9]+$"),
            "Expected Verification.Dapp.Screen Code element to render a numeric code");
        Reporter.Log($"Unity-side verification code: {code}");

        Directory.CreateDirectory(Path.GetDirectoryName(CrossPlatformPaths.AuthVerificationCodePath)!);
        File.WriteAllText(CrossPlatformPaths.AuthVerificationCodePath, code);
        Reporter.Log($"Wrote code to {CrossPlatformPaths.AuthVerificationCodePath}");
    }
}
