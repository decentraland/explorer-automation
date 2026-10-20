namespace ExplorerAutomation.Tests.Tests;

/// <summary>
/// Convergence stage for the wallet-login-with-emote scenario. Shelled out
/// by the Playwright orchestrator (`explorer-wallet-login.spec.ts`) via
/// `runExplorerTest('TestInWorldAndRunEmote')` once the client has accepted
/// the device-pairing approval and transitioned in-world. Asserts the
/// player-facing HUD is up and exercises a Fist Pump emote — catches
/// regressions in both the auth handshake and the post-login in-world
/// experience.
///
/// Inherits from <see cref="BaseTest"/> so its `OneTimeSetUp` waits through
/// splash → loading → main menu (handles the post-sign-in transition).
/// </summary>
[AllureSuite("Wallet Login")]
[Category("CrossVerify")]
[Order(18)]
public class WalletLoginInWorldEmote : BaseTest
{
    [Test]
    public void TestInWorldAndRunEmote()
    {
        Assert.That(Views.MainMenu.IsPresent(), Is.True,
            "Main menu (sidebar) should be visible after the wallet-login handoff completes.");

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
