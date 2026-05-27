namespace ExplorerAutomation.Tests.Tests;

[AllureSuite("Explore Panel Tests")]
[Category("InWorld")]
[Order(11)]
public class ExplorePanelTests : BaseTest
{
    [Test]
    public void TestOpenEventsFromSidebar()
    {
        Views.MainMenu.EventsButton.Click();

        Views.ExplorePanel.Events.WaitFor();

        Views.ExplorePanel.Close();
    }

    [Test]
    public void TestOpenPlacesFromSidebar()
    {
        Views.MainMenu.PlacesButton.Click();

        Views.ExplorePanel.Places.WaitFor();

        Views.ExplorePanel.Close();
    }

    [Test]
    public void TestOpenCommunitiesFromSidebar()
    {
        Views.MainMenu.CommunitiesButton.Click();

        Views.ExplorePanel.Communities.WaitFor();

        Views.ExplorePanel.Close();
    }

    [Test]
    public void TestOpenBackpackFromSidebar()
    {
        Views.MainMenu.BackpackButton.Click();

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

        Views.MainMenu.GalleryButton.Click();

        Views.ExplorePanel.Gallery.WaitFor();

        Views.ExplorePanel.Close();
    }

    [Test]
    public void TestOpenSettingsFromSidebar()
    {
        Views.MainMenu.SettingsButton.Click();

        Views.ExplorePanel.Settings.WaitFor();

        Views.ExplorePanel.Close();
    }

    [Test]
    public void TestSwitchBetweenAllTabs()
    {
        // Open the panel via any sidebar button
        Views.MainMenu.EventsButton.Click();
        Views.ExplorePanel.WaitFor();

        // Events tab
        Views.ExplorePanel.EventsTabButton.Click();
        Views.ExplorePanel.Events.WaitFor();
        Reporter.Log("Events tab opened successfully");

        // Places tab
        Views.ExplorePanel.PlacesTabButton.Click();
        Views.ExplorePanel.Places.WaitFor();
        Reporter.Log("Places tab opened successfully");

        // Communities tab
        Views.ExplorePanel.CommunitiesTabButton.Click();
        Views.ExplorePanel.Communities.WaitFor();
        Reporter.Log("Communities tab opened successfully");

        // Map tab
        Views.ExplorePanel.MapTabButton.Click();
        Views.ExplorePanel.Navmap.WaitFor();
        Reporter.Log("Map tab opened successfully");

        // Backpack tab
        Views.ExplorePanel.BackpackTabButton.Click();
        Views.ExplorePanel.Backpack.WaitFor();
        Reporter.Log("Backpack tab opened successfully");

        // Gallery tab
        Views.ExplorePanel.GalleryTabButton.Click();
        Views.ExplorePanel.Gallery.WaitFor();
        Reporter.Log("Gallery tab opened successfully");

        // Settings tab
        Views.ExplorePanel.SettingsTabButton.Click();
        Views.ExplorePanel.Settings.WaitFor();
        Reporter.Log("Settings tab opened successfully");

        Views.ExplorePanel.Close();
    }
}