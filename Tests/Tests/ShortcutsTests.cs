namespace ExplorerAutomation.Tests.Tests;

[TestFixture]
[AllureSuite("Shortcuts Tests")]
public class ShortcutsTests : BaseTest
{
    [Test]
    public void TestOpenEventsWithShortcut()
    {
        PressKey(AltKeyCode.X);
        Views.ExplorePanel.WaitFor();

        Assert.That(Views.ExplorePanel.Events.IsPresent(), Is.True, "Events section should be visible after pressing X");
        Reporter.Log("Events section opened via shortcut");

        PressEscape();
        Views.ExplorePanel.WaitForGone();
    }

    [Test]
    public void TestOpenPlacesWithShortcut()
    {
        PressKey(AltKeyCode.Z);
        Views.ExplorePanel.WaitFor();

        Assert.That(Views.ExplorePanel.Places.IsPresent(), Is.True, "Places section should be visible after pressing Z");
        Reporter.Log("Places section opened via shortcut");

        PressEscape();
        Views.ExplorePanel.WaitForGone();
    }

    [Test]
    public void TestOpenCommunitiesWithShortcut()
    {
        PressKey(AltKeyCode.O);
        Views.ExplorePanel.WaitFor();

        Assert.That(Views.ExplorePanel.Communities.IsPresent(), Is.True, "Communities section should be visible after pressing O");
        Reporter.Log("Communities section opened via shortcut");

        PressEscape();
        Views.ExplorePanel.WaitForGone();
    }

    [Test]
    public void TestOpenMapWithShortcut()
    {
        PressKey(AltKeyCode.M);
        Views.ExplorePanel.WaitFor();

        Assert.That(Views.ExplorePanel.Navmap.IsPresent(), Is.True, "Navmap section should be visible after pressing M");
        Reporter.Log("Map section opened via shortcut");

        PressEscape();
        Views.ExplorePanel.WaitForGone();
    }

    [Test]
    public void TestOpenBackpackWithShortcut()
    {
        PressKey(AltKeyCode.I);
        Views.ExplorePanel.WaitFor();

        Assert.That(Views.ExplorePanel.Backpack.IsPresent(), Is.True, "Backpack section should be visible after pressing I");
        Reporter.Log("Backpack section opened via shortcut");

        PressEscape();
        Views.ExplorePanel.WaitForGone();
    }

    [Test]
    public void TestOpenGalleryWithShortcut()
    {
        PressKey(AltKeyCode.K);
        Views.ExplorePanel.WaitFor();

        Assert.That(Views.ExplorePanel.Gallery.IsPresent(), Is.True, "Gallery section should be visible after pressing K");
        Reporter.Log("Gallery section opened via shortcut");

        PressEscape();
        Views.ExplorePanel.WaitForGone();
    }

    [Test]
    public void TestOpenSettingsWithShortcut()
    {
        PressKey(AltKeyCode.P);
        Views.ExplorePanel.WaitFor();

        Assert.That(Views.ExplorePanel.Settings.IsPresent(), Is.True, "Settings section should be visible after pressing P");
        Reporter.Log("Settings section opened via shortcut");

        PressEscape();
        Views.ExplorePanel.WaitForGone();
    }
}
