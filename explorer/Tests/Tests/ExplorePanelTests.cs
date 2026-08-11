namespace ExplorerAutomation.Tests.Tests;

[AllureSuite("Explore Panel Tests")]
[Category("InWorld")]
[Order(11)]
public class ExplorePanelTests : BaseTest
{
    [Test]
    public void TestOpenEventsFromSidebar()
    {
        Views.MainMenu.EventsButton.Click(settleMs: 0);

        Views.ExplorePanel.Events.WaitFor();

        Views.ExplorePanel.Close();
    }

    [Test]
    public void TestOpenPlacesFromSidebar()
    {
        Views.MainMenu.PlacesButton.Click(settleMs: 0);

        Views.ExplorePanel.Places.WaitFor();

        Views.ExplorePanel.Close();
    }

    [Test]
    public void TestOpenCommunitiesFromSidebar()
    {
        Views.MainMenu.CommunitiesButton.Click(settleMs: 0);

        Views.ExplorePanel.Communities.WaitFor();

        Views.ExplorePanel.Close();
    }

    // NOTE: there is no TestOpenMapFromSidebar — this build's sidebar has no Map button
    // (verified via UiDump `--all` dumps of the sidebar). Map coverage lives in
    // ShortcutsTests.TestOpenMapWithShortcut and in TestSwitchBetweenAllTabs below (Map tab).

    [Test]
    public void TestOpenBackpackFromSidebar()
    {
        Views.MainMenu.BackpackButton.Click(settleMs: 0);

        Views.ExplorePanel.Backpack.WaitFor();

        Views.ExplorePanel.Close();
    }

    // Opening the Gallery section makes the Explorer touch ~/Downloads (Camera Reel storage,
    // see unity-explorer ReelCommonActions.cs), which on macOS triggers the system
    // "wants to access your Downloads folder" TCC dialog that steals focus and breaks
    // AltTester input. Windows has no equivalent TCC prompt, so the test can run there.
    [Test]
    public void TestOpenGalleryFromSidebar()
    {
        if (OperatingSystem.IsMacOS())
            Assert.Ignore("macOS TCC dialog for ~/Downloads steals focus");

        Views.MainMenu.GalleryButton.Click(settleMs: 0);

        Views.ExplorePanel.Gallery.WaitFor();

        Views.ExplorePanel.Close();
    }

    [Test]
    public void TestOpenSettingsFromSidebar()
    {
        Views.MainMenu.SettingsButton.Click(settleMs: 0);

        Views.ExplorePanel.Settings.WaitFor();

        Views.ExplorePanel.Close();
    }

    [Test]
    public void TestSwitchBetweenAllTabs()
    {
        Views.MainMenu.EventsButton.Click(settleMs: 0);
        Views.ExplorePanel.WaitFor();

        Views.ExplorePanel.EventsTabButton.Click(settleMs: 0);
        Views.ExplorePanel.Events.WaitFor();
        Reporter.Log("Events tab opened successfully");

        Views.ExplorePanel.PlacesTabButton.Click(settleMs: 0);
        Views.ExplorePanel.Places.WaitFor();
        Reporter.Log("Places tab opened successfully");

        Views.ExplorePanel.CommunitiesTabButton.Click(settleMs: 0);
        Views.ExplorePanel.Communities.WaitFor();
        Reporter.Log("Communities tab opened successfully");

        Views.ExplorePanel.MapTabButton.Click(settleMs: 0);
        Views.ExplorePanel.Navmap.WaitFor();
        Reporter.Log("Map tab opened successfully");

        Views.ExplorePanel.BackpackTabButton.Click(settleMs: 0);
        Views.ExplorePanel.Backpack.WaitFor();
        Reporter.Log("Backpack tab opened successfully");

        Views.ExplorePanel.GalleryTabButton.Click(settleMs: 0);
        Views.ExplorePanel.Gallery.WaitFor();
        Reporter.Log("Gallery tab opened successfully");

        Views.ExplorePanel.SettingsTabButton.Click(settleMs: 0);
        Views.ExplorePanel.Settings.WaitFor();
        Reporter.Log("Settings tab opened successfully");

        Views.ExplorePanel.Close();
    }
}