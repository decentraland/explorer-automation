namespace ExplorerAutomation.Tests.Tests;

[AllureSuite("Shortcuts Tests")]
[Category("InWorld")]
[Order(12)]
public class ShortcutsTests : BaseTest
{
    [Test]
    public void TestOpenEventsWithShortcut()
    {
        PressKey(AltKeyCode.X, delay: 0);
        Views.ExplorePanel.WaitFor();

        Assert.That(Views.ExplorePanel.Events.IsPresent(), Is.True, "Events section should be visible after pressing X");
        Reporter.Log("Events section opened via shortcut");

        PressEscape(delay: 0);
        Views.ExplorePanel.WaitForGone();
    }

    [Test]
    public void TestOpenPlacesWithShortcut()
    {
        PressKey(AltKeyCode.Z, delay: 0);

        Views.ExplorePanel.Places.WaitFor();

        PressEscape(delay: 0);
        Views.ExplorePanel.WaitForGone();
    }

    [Test]
    public void TestOpenCommunitiesWithShortcut()
    {
        PressKey(AltKeyCode.O, delay: 0);

        Views.ExplorePanel.Communities.WaitFor();

        PressEscape(delay: 0);
        Views.ExplorePanel.WaitForGone();
    }

    [Test]
    public void TestOpenMapWithShortcut()
    {
        PressKey(AltKeyCode.M, delay: 0);

        Views.ExplorePanel.Navmap.WaitFor();

        PressEscape(delay: 0);
        Views.ExplorePanel.WaitForGone();
    }

    [Test]
    public void TestOpenBackpackWithShortcut()
    {
        PressKey(AltKeyCode.I, delay: 0);

        Views.ExplorePanel.Backpack.WaitFor();

        PressEscape(delay: 0);
        Views.ExplorePanel.WaitForGone();
    }

    [Test]
    public void TestOpenGalleryWithShortcut()
    {
        PressKey(AltKeyCode.K, delay: 0);

        Views.ExplorePanel.Gallery.WaitFor();

        PressEscape(delay: 0);
        Views.ExplorePanel.WaitForGone();
    }

    [Test]
    public void TestOpenSettingsWithShortcut()
    {
        PressKey(AltKeyCode.P, delay: 0);

        Views.ExplorePanel.Settings.WaitFor();

        PressEscape(delay: 0);
        Views.ExplorePanel.WaitForGone();
    }
}
