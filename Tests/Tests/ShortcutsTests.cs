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

        Views.ExplorePanel.Places.WaitFor();

        PressEscape();
        Views.ExplorePanel.WaitForGone();
    }

    [Test]
    public void TestOpenCommunitiesWithShortcut()
    {
        PressKey(AltKeyCode.O);

        Views.ExplorePanel.Communities.WaitFor();

        PressEscape();
        Views.ExplorePanel.WaitForGone();
    }

    [Test]
    public void TestOpenMapWithShortcut()
    {
        PressKey(AltKeyCode.M);

        Views.ExplorePanel.Navmap.WaitFor();

        PressEscape();
        Views.ExplorePanel.WaitForGone();
    }

    [Test]
    public void TestOpenBackpackWithShortcut()
    {
        PressKey(AltKeyCode.I);

        Views.ExplorePanel.Backpack.WaitFor();

        PressEscape();
        Views.ExplorePanel.WaitForGone();
    }

    [Test]
    public void TestOpenGalleryWithShortcut()
    {
        PressKey(AltKeyCode.K);

        Views.ExplorePanel.Gallery.WaitFor();

        PressEscape();
        Views.ExplorePanel.WaitForGone();
    }

    [Test]
    public void TestOpenSettingsWithShortcut()
    {
        PressKey(AltKeyCode.P);

        Views.ExplorePanel.Settings.WaitFor();

        PressEscape();
        Views.ExplorePanel.WaitForGone();
    }
}
