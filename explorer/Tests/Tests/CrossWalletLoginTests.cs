namespace ExplorerAutomation.Tests.Tests;

/// <summary>
/// Shared file-paths the cross-stack `@cross` flows use to communicate between
/// the Unity Explorer (writer) and the Playwright orchestrator (reader). Both
/// files live in Unity's `Application.persistentDataPath`, which resolves to
/// `~/Library/Application Support/Decentraland/Explorer/` on macOS — the same
/// directory the Unity client uses for its other runtime state (analytics
/// queue, sentry, profile cache, etc.).
///
/// TS-side mirror: `getAuthUrlPath()` + `getAuthVerificationCodePath()` in
/// `web/tests/auth/helpers/auth-request-bridge.ts`.
/// </summary>
internal static class CrossPlatformPaths
{
    /// <summary>
    /// URL the Unity client opened for the wallet auth flow, written by the
    /// `#if ALTTESTER` hook in `UnityAppWebBrowser.OpenUrl`. The Playwright
    /// spec polls for this file's appearance and uses its contents to drive
    /// its own browser to the same target.
    /// </summary>
    public static string AuthUrlPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Library", "Application Support", "Decentraland", "Explorer",
            "auth-url.txt");

    /// <summary>
    /// Validation code surfaced on `Verification.Dapp.Screen`, written by
    /// the C# <see cref="WalletLoginCodeRead"/> stage after the browser side
    /// has reached `/auth/requests/<id>` (which triggers the auth-api
    /// `request-validation-status` event that activates the Unity-side
    /// screen). Playwright reads this and asserts it equals the code
    /// rendered on the web RequestPage — the actual device-pairing safety
    /// check end-users perform visually.
    /// </summary>
    public static string AuthVerificationCodePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Library", "Application Support", "Decentraland", "Explorer",
            "auth-verification-code.txt");
}

/// <summary>
/// Stage 1 of the cross-stack wallet-login flow — fire the wallet auth flow
/// and confirm the client opened the system-browser URL (intercepted under
/// `#if ALTTESTER` to a file). Runs against a freshly-launched (logged-out)
/// Explorer. Extends <see cref="LoggedOutAuthBaseTest"/> so its
/// `EnsureInWorld` override lands the client at the LoginSelection screen
/// regardless of starting state.
///
/// Clicks the Metamask button. The client opens a SocketIO websocket to
/// auth-api, posts a `dcl_personal_sign` request, receives back
/// `{ requestId, code }`, and calls `webBrowser.OpenUrl(...)`. In ALTTESTER
/// builds the URL is written to <see cref="CrossPlatformPaths.AuthUrlPath"/>
/// instead of opening the system browser. This fixture asserts the file
/// appears within a generous timeout — the Playwright spec then reads the
/// file, derives the requestId, and drives its own browser to the same
/// `/auth/requests/<id>` URL.
///
/// We intentionally do NOT wait for `Verification.Dapp.Screen` here: that
/// screen only appears AFTER the server emits a `request-validation-status`
/// SocketIO event, which only happens once the browser side reaches the
/// requests page. Waiting on it would deadlock with the Playwright
/// orchestrator (which is itself waiting for this stage to complete).
/// Reading the code is the job of <see cref="WalletLoginCodeRead"/>.
/// </summary>
[AllureSuite("Wallet Login")]
[Category("CrossVerify")]
[Order(16)]
public class WalletLoginCapture : LoggedOutAuthBaseTest
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
        // it the moment Application.OpenURL would be called — typically 1-3
        // seconds after the click (auth-api websocket connect + dcl_personal_sign
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
/// Stage 2 of the cross-stack wallet-login flow — read the validation code
/// from Unity's `Verification.Dapp.Screen` once the auth-api has emitted the
/// `request-validation-status` event (i.e. once the browser side has reached
/// `/auth/requests/<id>`). Writes the code to
/// <see cref="CrossPlatformPaths.AuthVerificationCodePath"/> so Playwright can
/// read and compare it against the code rendered on the web RequestPage.
///
/// This stage exists *because* the device-pairing safety check is the actual
/// product feature being tested: client and web display the same code, the
/// user verifies they match before approving. Skipping it (clicking Approve
/// immediately) makes the test green but doesn't validate the security
/// property real users rely on.
/// </summary>
[AllureSuite("Wallet Login")]
[Category("CrossVerify")]
[Order(17)]
public class WalletLoginCodeRead : LoggedOutAuthBaseTest
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
