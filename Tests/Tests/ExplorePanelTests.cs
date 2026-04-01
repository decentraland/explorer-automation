namespace ExplorerAutomation.Tests.Tests;

[TestFixture]
[AllureSuite("Explore Panel Tests")]
public class ExplorePanelTests : BaseTest
{
    [Test]
    public void TestOpenEventsFromSidebar()
    {
        Views.MainMenu.EventsButton.Click();
        Views.ExplorePanel.WaitFor();

        Assert.That(Views.ExplorePanel.Events.IsPresent(), Is.True, "Events section should be visible");
        Reporter.Log("Events section opened successfully");

        Views.ExplorePanel.CloseButton.Click();
        Views.ExplorePanel.WaitForGone();
    }

    [Test]
    public void TestOpenPlacesFromSidebar()
    {
        Views.MainMenu.PlacesButton.Click();
        Views.ExplorePanel.WaitFor();

        Assert.That(Views.ExplorePanel.Places.IsPresent(), Is.True, "Places section should be visible");
        Reporter.Log("Places section opened successfully");

        Views.ExplorePanel.CloseButton.Click();
        Views.ExplorePanel.WaitForGone();
    }

    [Test]
    public void TestOpenCommunitiesFromSidebar()
    {
        Views.MainMenu.CommunitiesButton.Click();
        Views.ExplorePanel.WaitFor();

        Assert.That(Views.ExplorePanel.Communities.IsPresent(), Is.True, "Communities section should be visible");
        Reporter.Log("Communities section opened successfully");

        Views.ExplorePanel.CloseButton.Click();
        Views.ExplorePanel.WaitForGone();
    }

    [Test]
    public void TestOpenMapFromSidebar()
    {
        Views.MainMenu.MapButton.Click();
        Views.ExplorePanel.WaitFor();

        Assert.That(Views.ExplorePanel.Navmap.IsPresent(), Is.True, "Navmap section should be visible");
        Reporter.Log("Map section opened successfully");

        Views.ExplorePanel.CloseButton.Click();
        Views.ExplorePanel.WaitForGone();
    }

    [Test]
    public void TestOpenBackpackFromSidebar()
    {
        Views.MainMenu.BackpackButton.Click();
        Views.ExplorePanel.WaitFor();

        Assert.That(Views.ExplorePanel.Backpack.IsPresent(), Is.True, "Backpack section should be visible");
        Reporter.Log("Backpack section opened successfully");

        Views.ExplorePanel.CloseButton.Click();
        Views.ExplorePanel.WaitForGone();
    }

    [Test]
    public void TestOpenGalleryFromSidebar()
    {
        Views.MainMenu.GalleryButton.Click();
        Views.ExplorePanel.WaitFor();

        Assert.That(Views.ExplorePanel.Gallery.IsPresent(), Is.True, "Gallery section should be visible");
        Reporter.Log("Gallery section opened successfully");

        Views.ExplorePanel.CloseButton.Click();
        Views.ExplorePanel.WaitForGone();
    }

    [Test]
    public void TestOpenSettingsFromSidebar()
    {
        Views.MainMenu.SettingsButton.Click();
        Views.ExplorePanel.WaitFor();

        Assert.That(Views.ExplorePanel.Settings.IsPresent(), Is.True, "Settings section should be visible");
        Reporter.Log("Settings section opened successfully");

        Views.ExplorePanel.CloseButton.Click();
        Views.ExplorePanel.WaitForGone();
    }

    [Test]
    public void TestSwitchBetweenAllTabs()
    {
        // Open the panel via any sidebar button
        Views.MainMenu.EventsButton.Click();
        Views.ExplorePanel.WaitFor();

        // Events tab
        Views.ExplorePanel.EventsTab.Click();
        Assert.That(Views.ExplorePanel.Events.IsPresent(), Is.True, "Events section should be visible after clicking Events tab");
        Reporter.Log("Events tab opened successfully");

        // Places tab
        Views.ExplorePanel.PlacesTab.Click();
        Assert.That(Views.ExplorePanel.Places.IsPresent(), Is.True, "Places section should be visible after clicking Places tab");
        Reporter.Log("Places tab opened successfully");

        // Communities tab
        Views.ExplorePanel.CommunitiesTab.Click();
        Assert.That(Views.ExplorePanel.Communities.IsPresent(), Is.True, "Communities section should be visible after clicking Communities tab");
        Reporter.Log("Communities tab opened successfully");

        // Map tab
        Views.ExplorePanel.MapTab.Click();
        Assert.That(Views.ExplorePanel.Navmap.IsPresent(), Is.True, "Navmap section should be visible after clicking Map tab");
        Reporter.Log("Map tab opened successfully");

        // Backpack tab
        Views.ExplorePanel.BackpackTab.Click();
        Assert.That(Views.ExplorePanel.Backpack.IsPresent(), Is.True, "Backpack section should be visible after clicking Backpack tab");
        Reporter.Log("Backpack tab opened successfully");

        // Gallery tab
        Views.ExplorePanel.GalleryTab.Click();
        Assert.That(Views.ExplorePanel.Gallery.IsPresent(), Is.True, "Gallery section should be visible after clicking Gallery tab");
        Reporter.Log("Gallery tab opened successfully");

        // Settings tab
        Views.ExplorePanel.SettingsTab.Click();
        Assert.That(Views.ExplorePanel.Settings.IsPresent(), Is.True, "Settings section should be visible after clicking Settings tab");
        Reporter.Log("Settings tab opened successfully");

        Views.ExplorePanel.CloseButton.Click();
        Views.ExplorePanel.WaitForGone();
    }
}
