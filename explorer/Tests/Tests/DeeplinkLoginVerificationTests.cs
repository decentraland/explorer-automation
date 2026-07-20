namespace ExplorerAutomation.Tests.Tests;

/// <summary>
/// Fixture invoked from the TypeScript / Playwright `@cross` deeplink suite.
///
///     dotnet test explorer/Tests --filter "ClassName~DeeplinkLoginVerificationTests"
///
/// After the deeplink login completes, the Explorer does NOT auto-jump into the
/// world. It stays on the auth screen in the cached-account state ("Jump Into
/// Decentraland" button visible). The user must click that button to proceed.
///
/// This fixture overrides <see cref="BaseTest.EnsureInWorld"/> to explicitly
/// verify the deeplink-specific intermediate state before clicking JumpIn:
///   1. Splash clears
///   2. Auth screen appears with JumpIntoWorldButton (cached-account state)
///   3. LoginSelectionScreen is NOT present (we're not on the login form)
///   4. Click JumpIntoWorldButton
///   5. World loads → main menu visible
///
/// Individual tests then verify the resulting authenticated session.
/// </summary>
[AllureSuite("Deeplink Login Verification")]
[Category("CrossVerify")]
[Order(16)]
public class DeeplinkLoginVerificationTests : BaseTest
{
    protected override void EnsureInWorld()
    {
        if (Views.SplashScreen.IsPresent())
        {
            Reporter.Log("Splash screen detected — waiting for it to clear");
            Views.SplashScreen.WaitForGone(60);
        }

        // After deeplink login, the Explorer must land on the cached-account auth
        // screen — NOT the login form. If we see LoginSelectionScreen instead, the
        // deeplink identity was not consumed and we're in a logged-out state.
        Views.AuthenticationMainScreen.WaitFor(60);
        Reporter.Log("Auth screen detected after deeplink login");

        Assert.That(Views.AuthenticationMainScreen.JumpIntoWorldButton.IsPresent(), Is.True,
            "After deeplink login the auth screen should show 'Jump Into Decentraland' (cached-account state), "
            + "not the login form. If this fails, the deeplink identity was not consumed by the Explorer.");

        Assert.That(Views.AuthenticationMainScreen.LoginSelectionScreen.IsPresent(), Is.False,
            "The login form (email / MetaMask / Google) should NOT be visible — the deeplink login "
            + "should have cached the account, showing the Jump In screen instead.");

        Reporter.Log("Cached-account state confirmed — clicking Jump Into Decentraland");
        Views.AuthenticationMainScreen.JumpIntoWorldButton.Click();

        try
        {
            Views.LoadingScreen.WaitFor(15);
            Reporter.Log("Scene loading screen visible — waiting for world streaming to finish (up to 5 min)");
            Views.LoadingScreen.WaitForGone(300);
            Reporter.Log("Scene loading complete");
        }
        catch (Exception)
        {
            Reporter.Log("Scene loading screen never appeared — assuming world was already loaded");
        }

        Views.MainMenu.WaitFor(120);
        Thread.Sleep(20_000);
        Reporter.Log("Player is in-world and main menu is ready");
    }

    [Test]
    public void TestExplorerIsInWorldFromDeeplinkLogin()
    {
        Assert.That(Views.MainMenu.IsPresent(), Is.True,
            "Main menu (sidebar) should be visible after clicking Jump Into Decentraland.");
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
