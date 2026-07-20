namespace ExplorerAutomation.Tests.Tests;

/// <summary>
/// Fixture invoked from the TypeScript / Playwright `@cross` deeplink suite to
/// verify that an Explorer launched via the auth-token-bridge handoff (fed by the
/// deeplink auth flow) actually reaches the in-world state. The TS test shells out:
///
///     dotnet test explorer/Tests --filter "Name=TestExplorerIsInWorldFromDeeplinkLogin"
///
/// The deeplink flow produces an identity ID that Playwright writes to
/// auth-token-bridge.txt. TokenFileAuthenticator picks it up, fetches the identity,
/// and auto-logs in — same mechanism as the standard token bridge, just sourced from
/// the deeplink auth flow instead of the web dapp.
/// </summary>
[AllureSuite("Deeplink Login Verification")]
[Category("CrossVerify")]
[Order(16)]
public class DeeplinkLoginVerificationTests : BaseTest
{
    [Test]
    public void TestExplorerIsInWorldFromDeeplinkLogin()
    {
        Assert.That(Views.MainMenu.IsPresent(), Is.True,
            "Main menu (sidebar) should be visible after deeplink login via token bridge.");
    }
}
