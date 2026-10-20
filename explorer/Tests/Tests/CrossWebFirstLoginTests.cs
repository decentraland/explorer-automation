namespace ExplorerAutomation.Tests.Tests;

/// <summary>
/// Shared file path used by the cross-stack web-first signup flow (Flow 2)
/// to communicate the expected in-world username from the Playwright
/// orchestrator (writer) to this C# fixture (reader).
///
/// Lives in `~/Library/Application Support/DecentralandLauncherLight/` —
/// the same directory the desktop launcher uses for its own state and
/// where it consumes `auth-token-bridge.txt`. Keeping all cross-stack
/// communication files in one well-known dir mirrors what Flow 1 does
/// with the Explorer's `Application.persistentDataPath` for its own
/// `auth-url.txt` / `auth-verification-code.txt` pair.
///
/// TS-side mirror: `getExpectedUsernamePath()` /
/// `writeExpectedUsername()` in
/// `web/tests/auth/helpers/token-bridge.ts`.
/// </summary>
internal static class CrossWebFirstLoginPaths
{
    public static string ExpectedUsernamePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Library", "Application Support", "DecentralandLauncherLight",
            "expected-username.txt");
}

/// <summary>
/// Flow 2 — Stage 5a, identity guard. The launcher consumes
/// <c>auth-token-bridge.txt</c> on startup and the Explorer authenticates
/// against catalysts. This fixture reads the expected username the
/// Playwright spec wrote to <see cref="CrossWebFirstLoginPaths.ExpectedUsernamePath"/>
/// before launching the client, polls until the chat-panel titlebar's
/// <c>[TXT] UserName</c> label matches it (the catalyst profile fetch is
/// asynchronous; the label updates only after auth + profile resolve), and
/// fails fast with diagnostic output on timeout.
///
/// The assertion is load-bearing: a raw "Explorer reached in-world" alone
/// can pass on a launcher that silently consumed a stale bridge file or
/// booted a profile cache from a prior run with a different identity. Both
/// manifest as "main menu visible + emote plays" with the wrong account,
/// masking real auth-handoff regressions. Comparing the displayed username
/// to the QuickSetup username is the cheap, deterministic way to catch
/// that class of cache-pollution bugs.
///
/// Inherits from <see cref="BaseTest"/> but overrides
/// <see cref="BaseTest.EnsureInWorld"/> to avoid the default implementation's
/// immediate-click on <c>JumpIntoWorldButton</c> when it sees the auth
/// screen — that would ride past whatever state we need to read. Our
/// override only waits for splash to clear; the test body owns the polling.
///
/// The subsequent <c>WalletLoginInWorldEmote</c> fixture (Order 18, separate
/// <c>dotnet test</c> invocation) inherits the standard <c>EnsureInWorld</c>
/// which clicks Jump and rides to in-world if needed.
/// </summary>
[AllureSuite("Web-First Login")]
[Category("CrossVerify")]
[Order(17)]
public class WebFirstLoginUsernameAssert : BaseTest
{
    /// <summary>
    /// Wait for splash only; defer screen-state assertions to the [Test]
    /// body. We can't pin to a single screen here because Flow 2's
    /// token-bridge boot can legitimately land on either the cached-account
    /// screen ("Welcome &lt;name&gt;" + JumpIntoWorld button) OR auto-ride
    /// past it depending on launcher behavior and account state.
    /// </summary>
    protected override void EnsureInWorld()
    {
        if (Views.SplashScreen.IsPresent())
        {
            Reporter.Log("Splash screen detected — waiting for it to clear");
            Views.SplashScreen.WaitForGone(60);
        }
    }

    [Test]
    public void TestInWorldUsernameMatches()
    {
        Assert.That(File.Exists(CrossWebFirstLoginPaths.ExpectedUsernamePath), Is.True,
            $"Expected the Playwright orchestrator to write the expected username to " +
            $"{CrossWebFirstLoginPaths.ExpectedUsernamePath} before launching the client. " +
            "Without it, this fixture has nothing to compare against.");

        var expected = File.ReadAllText(CrossWebFirstLoginPaths.ExpectedUsernamePath).Trim();
        Assert.That(expected, Is.Not.Empty,
            "expected-username.txt was written but is empty.");

        // The Lobby.ExistingAccount.Screen `Title` element renders either
        // "Welcome <username>" (first-launch / new account just minted via
        // the auth-token-bridge) or "Welcome back <username>" (returning
        // account whose profile already exists on catalysts). Both forms
        // are valid evidence the bridge token landed on the right identity.
        // Catalyst profile fetch is asynchronous, so the label only
        // resolves once the profile data is back — 180s covers cold-cache
        // lookups for brand-new accounts on CI.
        var newUserGreeting = $"Welcome {expected}";
        var returningGreeting = $"Welcome back {expected}";
        var deadline = DateTime.UtcNow.AddSeconds(180);
        string lastSeen = null;
        while (DateTime.UtcNow < deadline)
        {
            try { lastSeen = Views.AuthenticationMainScreen.UsernameLabel.GetText(2); }
            catch { /* element not yet in scene — keep polling */ }

            var trimmed = lastSeen?.Trim();
            if (string.Equals(trimmed, newUserGreeting, StringComparison.Ordinal) ||
                string.Equals(trimmed, returningGreeting, StringComparison.Ordinal))
            {
                Reporter.Log($"Welcome-screen greeting matched '{trimmed}'");
                return;
            }
            Thread.Sleep(1000);
        }

        throw new AssertionException(
            $"Welcome-screen greeting was '{lastSeen}', expected '{newUserGreeting}' or " +
            $"'{returningGreeting}' within 180s. " +
            "Likely causes: (a) the launcher consumed a stale auth-token-bridge.txt from a " +
            "prior run, (b) the Explorer's Thirdweb identity cache shadowed the bridge token " +
            "(spec should clearExplorerIdentityCache + launch Explorer freshly), " +
            "(c) the dapp minted a download URL that didn't carry the just-signed-up identity, " +
            "or (d) catalyst profile resolution is slow / failing. " +
            "Run DiagnoseAuthScreenUsernameLabel to inspect what's actually rendered.");
    }
}
