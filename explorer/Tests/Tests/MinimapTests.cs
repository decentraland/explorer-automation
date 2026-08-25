namespace ExplorerAutomation.Tests.Tests;

// NOTE: no quest icon/sticker test — this build (dev_b97439fc) has no quest HUD at all
// (verified via UiDump: zero objects matching quest/reward names anywhere in the scene).
[AllureSuite("Minimap Tests")]
[Category("InWorld")]
[Order(13)]
public class MinimapTests : BaseTest
{
    [Test]
    public void TestMinimapHudPresence()
    {
        Views.Minimap.WaitFor();

        var placeName = Views.Minimap.PlaceName.GetText();
        Assert.That(placeName, Is.Not.Empty, "Minimap should display the current place name");
        Reporter.Log($"Minimap shows place: {placeName}");

        var coordinates = Views.Minimap.PlaceCoordinates.GetText();
        var parts = coordinates.Split(',');
        Assert.That(parts, Has.Length.EqualTo(2), $"Coordinates '{coordinates}' should be 'x, y'");
        Assert.That(int.TryParse(parts[0].Trim(), out _), Is.True, $"X coordinate in '{coordinates}' should be an integer");
        Assert.That(int.TryParse(parts[1].Trim(), out _), Is.True, $"Y coordinate in '{coordinates}' should be an integer");
        Reporter.Log($"Minimap shows coordinates: {coordinates}");

        Views.Minimap.MapRenderButton.WaitFor();
        Views.Minimap.CompassNorth.WaitFor();
        Reporter.Log("Minimap map render and compass are visible");
    }

    [Test]
    public void TestToggleFavoriteFromMinimap()
    {
        Views.Minimap.WaitFor();

        var initiallyFavorited = Views.Minimap.IsFavorited();
        Reporter.Log($"Initial favorite state: {initiallyFavorited}");

        Views.Minimap.ToggleFavorite();
        Assert.That(Views.Minimap.IsFavorited(), Is.EqualTo(!initiallyFavorited),
            "Favorite heart should flip state after clicking it");

        // Restore the original state so the shared test account keeps no stray favorites.
        Views.Minimap.ToggleFavorite();
        Assert.That(Views.Minimap.IsFavorited(), Is.EqualTo(initiallyFavorited),
            "Favorite heart should return to its original state after the second click");
        Reporter.Log("Favorite state restored to original");
    }

    [Test]
    public void TestOpenMinimapContextMenu()
    {
        Views.Minimap.WaitFor();

        Views.Minimap.OpenContextMenu();
        Views.Minimap.ContextMenu.SetAsHomeToggle.WaitFor();
        Reporter.Log("Set as Home toggle is visible");

        // The two button rows are pooled clones whose order is not contractual — assert the
        // label set instead of a per-index label.
        var labels = new[]
        {
            Views.Minimap.ContextMenu.ButtonLabels[0].GetText(),
            Views.Minimap.ContextMenu.ButtonLabels[1].GetText(),
        };
        Assert.That(labels, Does.Contain("Copy Link"), "Context menu should offer Copy Link");
        Assert.That(labels, Does.Contain("Reload Scene"), "Context menu should offer Reload Scene");
        Reporter.Log($"Context menu entries: {string.Join(", ", labels)}");

        PressEscape(delay: 0);
        Views.Minimap.ContextMenu.WaitForGone();
        Reporter.Log("Context menu closed with Escape");
    }

    [Test]
    public void TestCollapseAndExpandMinimap()
    {
        Views.Minimap.WaitFor();

        // Collapse and Expand are a swap-pair: only one of the two is active at a time,
        // so waiting for the counterpart proves the state actually changed.
        Views.Minimap.CollapseButton.Click(settleMs: 0);
        Views.Minimap.ExpandButton.WaitFor();
        Reporter.Log("Minimap collapsed");

        Views.Minimap.ExpandButton.Click(settleMs: 0);
        Views.Minimap.CollapseButton.WaitFor();
        Reporter.Log("Minimap expanded again");
    }

    [Test]
    public void TestOpenMapFromMinimap()
    {
        Views.Minimap.WaitFor();

        // Retry, not just settleMs: 0 + a single wait — a click this button drops leaves
        // no other signal to recover from than clicking it again.
        ClickUntil(() => Views.Minimap.MapRenderButton.Click(settleMs: 0),
                   () => Views.ExplorePanel.Navmap.IsPresent(verificationShot: false));
        Views.ExplorePanel.Navmap.WaitFor();
        Reporter.Log("Clicking the minimap opened the full navmap");

        Views.ExplorePanel.Close();
    }
}
