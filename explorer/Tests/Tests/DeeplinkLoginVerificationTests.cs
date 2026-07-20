namespace ExplorerAutomation.Tests.Tests;

/// <summary>
/// Fixture invoked from the TypeScript / Playwright `@cross` deeplink suite to
/// verify that an Explorer launched with a pre-written deeplink-bridge.json
/// actually reaches the in-world state. The TS test shells out:
///
///     dotnet test explorer/Tests --filter "Name=TestExplorerIsInWorldFromDeeplinkLogin"
///
/// The real flow: DappDeepLinkAuthenticator generates an authRequestId, opens
/// the browser, and waits for DeeplinkSentinel to deliver a matching signin
/// via deeplink-bridge.json. The Playwright @cross test completes the browser
/// auth, captures the deep link URL, and writes deeplink-bridge.json in the
/// format {"deeplink":"decentraland://open?signin={id}&amp;authRequestId={uuid}"}.
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
            "Main menu (sidebar) should be visible after deeplink login completes.");
    }
}
