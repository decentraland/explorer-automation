namespace ExplorerAutomation.Tests.Tests;

/// <summary>
/// Fixture invoked from the TypeScript / Playwright `@cross` deeplink suite to
/// verify that an Explorer launched after a deeplink login flow has a fully
/// functional authenticated session. The TS test shells out:
///
///     dotnet test explorer/Tests --filter "ClassName~DeeplinkLoginVerificationTests"
///
/// Flow: Playwright completes the browser-side deeplink auth, captures the
/// deep link URL (decentraland://open?signin={identityId}&amp;authRequestId={uuid}),
/// writes deeplink-bridge.json, and launches the Explorer with AltTester.
/// EnsureInWorld (inherited from BaseTest) handles splash → auth → loading.
/// These tests then verify the resulting session.
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

    [Test]
    public void TestProfileMenuAccessibleAfterDeeplinkLogin()
    {
        Views.MainMenu.ProfileButton.Click();
        Views.ProfileMenu.WaitFor(15);

        Assert.That(Views.ProfileMenu.SignOutButton.IsPresent(), Is.True,
            "Sign Out button should be present — proves the session is authenticated, not anonymous.");

        PressEscape();
    }

    [Test]
    public void TestBackpackAccessibleAfterDeeplinkLogin()
    {
        Views.MainMenu.BackpackButton.Click();
        Views.ExplorePanel.WaitFor();

        Assert.That(Views.ExplorePanel.Backpack.IsPresent(), Is.True,
            "Backpack section should load after clicking the sidebar button.");

        Views.ExplorePanel.Close();
    }

    [Test]
    public void TestMapAccessibleAfterDeeplinkLogin()
    {
        Views.MainMenu.MapButton.Click();
        Views.ExplorePanel.WaitFor();

        Assert.That(Views.ExplorePanel.Navmap.IsPresent(), Is.True,
            "Navmap section should load — proves spatial navigation is available post-login.");

        Views.ExplorePanel.Close();
    }
}
