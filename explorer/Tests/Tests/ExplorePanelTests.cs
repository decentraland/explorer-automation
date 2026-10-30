namespace ExplorerAutomation.Tests.Tests;

[AllureSuite("Explore Panel Tests")]
[Category("InWorld")]
[Order(11)]
public class ExplorePanelTests : BaseTest
{
    [Test]
    public void TestOpenEventsFromSidebar()
    {
        Views.MainMenu.OpenFromDiscover(Views.MainMenu.Discover.EventsOption);

        Views.ExplorePanel.Events.WaitFor();

        Views.ExplorePanel.Close();
    }

    [Test]
    public void TestOpenPlacesFromSidebar()
    {
        Views.MainMenu.OpenFromDiscover(Views.MainMenu.Discover.PlacesOption);

        Views.ExplorePanel.Places.WaitFor();

        Views.ExplorePanel.Close();
    }

    [Test]
    public void TestOpenCommunitiesFromSidebar()
    {
        Views.MainMenu.OpenFromDiscover(Views.MainMenu.Discover.CommunitiesOption);

        Views.ExplorePanel.Communities.WaitFor();

        Views.ExplorePanel.Close();
    }

    [Test]
    public void TestOpenBackpackFromSidebar()
    {
        Views.MainMenu.OpenSubMenu(Views.MainMenu.ProfileButton, Views.ProfileMenu);
        Views.ProfileMenu.BackpackButton.Click(settleMs: 0);

        Views.ExplorePanel.Backpack.WaitFor();

        Views.ExplorePanel.Close();
    }

    [Test]
    public void TestOpenGalleryFromSidebar()
    {
        Views.MainMenu.OpenFromGallery(Views.MainMenu.Gallery.GalleryOption);

        Views.ExplorePanel.Gallery.WaitFor();

        Views.ExplorePanel.Close();
    }

    [Test]
    public void TestOpenSettingsFromSidebar()
    {
        Views.MainMenu.OpenFromSystem(Views.MainMenu.System.SettingsOption);

        Views.ExplorePanel.Settings.WaitFor();

        Views.ExplorePanel.Close();
    }

    [Test]
    public void TestSwitchBetweenAllTabs()
    {
        Views.MainMenu.OpenFromDiscover(Views.MainMenu.Discover.EventsOption);
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