namespace ExplorerAutomation.Tests.Tests;

// Send/accept/decline and the connected-users list need a second logged-in account and are
// not automated here (same constraint as ChatTests' DM/mention notes). Coverage below is
// depth on the panel structure itself: open/close and the three tabs.
// The in-world Order band 10-19 is full, so this fixture shares Order 19 with
// CameraTests/EmoteWheelTests/SkyboxTests (duplicate Orders have precedent — see
// explorer/README.md's fixture ordering invariant).
[AllureSuite("Friends Tests")]
[Category("InWorld")]
[Order(19)]
public class FriendsTests : BaseTest
{
    [Test]
    public void TestOpenAndCloseFriendsPanel()
    {
        if (!FeatureFlags.IsFeatureEnabled("Friends"))
            Assert.Ignore("Friends is off in this environment");

        OpenFriends();

        Assert.That(Views.MainMenu.Friends.FriendsTabButton.IsPresent(), Is.True,
            "Friends tab button should be visible");
        Assert.That(Views.MainMenu.Friends.RequestsTabButton.IsPresent(), Is.True,
            "Requests tab button should be visible");
        Assert.That(Views.MainMenu.Friends.BlockedTabButton.IsPresent(), Is.True,
            "Blocked tab button should be visible");
        Reporter.Log("Friends panel opened with all three tabs present");

        Views.MainMenu.Friends.CloseButton.Click();
        Views.MainMenu.Friends.WaitForGone();
        Reporter.Log("Friends panel closed via its close button");
    }

    [Test]
    public void TestCloseFriendsPanelWithEscape()
    {
        if (!FeatureFlags.IsFeatureEnabled("Friends"))
            Assert.Ignore("Friends is off in this environment");

        OpenFriends();

        PressEscape();
        Views.MainMenu.Friends.WaitForGone();
        Reporter.Log("Friends panel closed with Escape");
    }

    [Test]
    public void TestSwitchBetweenFriendsRequestsAndBlockedTabs()
    {
        if (!FeatureFlags.IsFeatureEnabled("Friends"))
            Assert.Ignore("Friends is off in this environment");

        OpenFriends();

        // The Friends tab's empty state ("Time To Make Some Friends!") is only present when
        // the account has no friends yet — read it rather than assuming, so this test still
        // means something once the shared identity has accumulated any.
        var startsEmpty = Views.MainMenu.Friends.EmptyStateTitle.IsPresent();
        Reporter.Log($"Friends tab empty state on entry: {startsEmpty}");

        Views.MainMenu.Friends.RequestsTabButton.Click();
        Wait(1);
        Assert.That(Views.MainMenu.Friends.EmptyStateTitle.IsPresent(), Is.False,
            "Friends tab's empty-state message should not show while viewing Requests");
        Reporter.Log("Switched to Requests tab");

        // Blocked reuses the same MainTitle element for its own empty state (different text,
        // not different structure like Requests) — verified live: "No Blocked Accounts".
        Views.MainMenu.Friends.BlockedTabButton.Click();
        Wait(1);
        Assert.That(Views.MainMenu.Friends.EmptyStateTitle.GetText(), Is.EqualTo("No Blocked Accounts"),
            "Blocked tab should show its own empty-state message, not the Friends tab's");
        Reporter.Log("Switched to Blocked tab");

        Views.MainMenu.Friends.FriendsTabButton.Click();
        Wait(1);
        Assert.That(Views.MainMenu.Friends.EmptyStateTitle.IsPresent(), Is.EqualTo(startsEmpty),
            "Friends tab should show the same empty-state as on entry after switching back");
        Reporter.Log("Switched back to Friends tab");

        Views.MainMenu.Friends.CloseButton.Click();
        Views.MainMenu.Friends.WaitForGone();
    }

    /// <summary>
    /// Opens the Friends panel via the sidebar button. The first click of a run can be
    /// dropped (same class of race as MinimapTests.TestOpenMapFromMinimap — see PR #82),
    /// so retry rather than fail outright.
    /// </summary>
    private void OpenFriends()
    {
        ClickUntil(() => Views.MainMenu.FriendsButton.Click(),
                   () => Views.MainMenu.Friends.IsPresent());
        Views.MainMenu.Friends.WaitFor();
    }
}
