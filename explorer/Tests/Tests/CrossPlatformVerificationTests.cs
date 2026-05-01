namespace ExplorerAutomation.Tests.Tests;

/// <summary>
/// Fixture invoked from the TypeScript / Playwright `@cross` suite to verify
/// that an Explorer launched via the auth-token-bridge handoff actually reaches
/// the in-world state. The TS test shells out via:
///
///     dotnet test explorer/Tests --filter "Name=TestExplorerIsInWorldFromTokenBridge"
///
/// `EnsureInWorld()` (inherited from BaseTest) handles splash → loading. Once
/// it returns, `Views.MainMenu.IsPresent()` confirms the player-facing HUD
/// rendered.
/// </summary>
[AllureSuite("Cross-Platform Verification")]
[Category("CrossVerify")]
public class CrossPlatformVerificationTests : BaseTest
{
    [Test]
    public void TestExplorerIsInWorldFromTokenBridge()
    {
        Assert.That(Views.MainMenu.IsPresent(), Is.True,
            "Main menu (sidebar) should be visible after token-bridge auto-login completes.");
    }
}
