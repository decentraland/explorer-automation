namespace ExplorerAutomation.Tests.Tests;

[AllureSuite("Shortcuts Tests")]
[Category("InWorld")]
[Order(12)]
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

    // Same reason as TestOpenGalleryFromSidebar: opening Gallery touches ~/Downloads (Camera
    // Reel storage in unity-explorer ReelCommonActions.cs) and triggers macOS's TCC dialog,
    // which steals focus from the Explorer window. Windows has no equivalent TCC prompt,
    // so the test can run there.
    [Test]
    public void TestOpenGalleryWithShortcut()
    {
        if (OperatingSystem.IsMacOS())
            Assert.Ignore("macOS TCC dialog for ~/Downloads steals focus");

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
