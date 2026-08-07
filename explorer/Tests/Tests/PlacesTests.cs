namespace ExplorerAutomation.Tests.Tests;

// Depth coverage for the explore panel's Places section (the open-from-sidebar smoke test
// lives in ExplorePanelTests). The in-world Order band 10-19 is full, so this fixture
// shares Order 11 with ExplorePanelTests (duplicate Orders have precedent at 16 and 19).
[AllureSuite("Places Tests")]
[Category("InWorld")]
[Order(11)]
public class PlacesTests : BaseTest
{
    // Grace given to a Click before falling back to Tap — long enough that a working-but-slow
    // open is not second-guessed, short enough to leave room for the authoritative wait.
    private const double DETAIL_OPEN_GRACE = 15;

    [Test]
    public void TestSwitchPlacesTabs()
    {
        OpenPlaces();

        Views.ExplorePanel.Places.RecentTabButton.Click();
        Wait(1); // the counter enables before its text is refreshed
        Assert.That(Views.ExplorePanel.Places.ResultsCounter.GetText(), Does.StartWith("Recent"),
            "Recent tab should show the 'Recent (N)' results counter");
        Reporter.Log("Recent tab opened");

        Views.ExplorePanel.Places.FavoritesTabButton.Click();
        Wait(1);
        Assert.That(Views.ExplorePanel.Places.ResultsCounter.GetText(), Does.StartWith("Favorites"),
            "Favorites tab should show the 'Favorites (N)' results counter");
        Reporter.Log("Favorites tab opened");

        Views.ExplorePanel.Places.MyPlacesTabButton.Click();
        Wait(1);
        Assert.That(Views.ExplorePanel.Places.ResultsCounter.GetText(), Does.StartWith("My Places"),
            "My Places tab should show the 'My Places (N)' results counter");
        Reporter.Log("My Places tab opened");

        Views.ExplorePanel.Places.ExploreTabButton.Click();
        Views.ExplorePanel.Places.ResultsCounter.WaitForGone();
        Reporter.Log("Explore tab restored — counter hidden");

        Views.ExplorePanel.Close();
    }

    [Test]
    public void TestSearchPlaces()
    {
        OpenPlaces();

        Views.ExplorePanel.Places.SearchBar.SetText("Genesis Plaza");
        Wait(2); // wait for the remote search to resolve

        Assert.That(Views.ExplorePanel.Places.ResultsCounter.GetText(),
            Does.StartWith("Results for 'Genesis Plaza'"),
            "Search should show a 'Results for ...' counter");
        Assert.That(Views.ExplorePanel.Places.Cards[0].PlaceName.GetText(),
            Does.Contain("Genesis Plaza"),
            "First search result should match the query");
        Reporter.Log("Search for 'Genesis Plaza' returned matching results");

        Views.ExplorePanel.Places.ClearSearchButton.Click();
        Views.ExplorePanel.Places.ResultsCounter.WaitForGone();
        Reporter.Log("Search cleared");

        Views.ExplorePanel.Close();
    }

    [Test]
    public void TestOpenPlaceDetail()
    {
        OpenPlaces();

        var placeName = Views.ExplorePanel.Places.Cards[0].PlaceName.GetText();
        // Click the thumbnail, not the card root — the hover overlay puts JUMP IN and the
        // like/heart/home buttons at the card's center (see PlaceCard doc comment).
        // Click first, Tap on no response: Click moves the pointer onto the card before
        // pressing, and on a slow chassis the card's hover overlay renders into that gap and
        // swallows the press — 60s waits and repeated clicks never opened the panel on CI
        // run 31176916555. Tap presses the thumbnail directly and raises no overlay.
        Views.ExplorePanel.Places.Cards[0].Thumbnail.ClickOrTap(
            () => Views.ExplorePanel.Places.PlaceDetail.IsPresent(verificationShot: false),
            graceSeconds: DETAIL_OPEN_GRACE);

        Views.ExplorePanel.Places.PlaceDetail.WaitFor(SlowChassis.SETTLE_TIMEOUT);
        Assert.That(Views.ExplorePanel.Places.PlaceDetail.PlaceTitle.GetText(), Is.EqualTo(placeName),
            "Place detail title should match the clicked card");
        Reporter.Log($"Place detail opened for '{placeName}'");

        Views.ExplorePanel.Places.PlaceDetail.CloseButton.Click();
        Views.ExplorePanel.Places.PlaceDetail.WaitForGone();

        Views.ExplorePanel.Close();
    }

    [Test]
    public void TestFilterPlacesByCategory()
    {
        OpenPlaces();

        var firstBefore = Views.ExplorePanel.Places.Cards[0].PlaceName.GetText();

        // Chip order: ALL, SOCIAL, MUSIC, ART, GAME, FASHION, EDUCATION, SHOP, SPORTS, BUSINESS.
        // Tap, not Click: the chips ignore the synthetic Click event (verified live — Click
        // reports success but ALL stays selected; Tap selects the chip).
        var category = Views.ExplorePanel.Places.CategoryLabels[3].GetText();
        Views.ExplorePanel.Places.CategoryButtons[3].Tap();
        Reporter.Log($"Selected category chip '{category}'");

        // The chips carry no readable selected-state, and interacting with cards on the
        // freshly filtered grid is a minefield (plain Click is a no-op; a retried hover+tap
        // can land on the hover overlay's JUMP IN and teleport the player). Verify the
        // filter by its observable effect instead: the leading card changes once the
        // category's results load — the same signal the pagination tests rely on.
        // Detail-open coverage lives in TestOpenPlaceDetail on the default grid.
        var firstAfter = firstBefore;
        for (var attempt = 0; attempt < 20 && firstAfter == firstBefore; attempt++)
        {
            Wait(0.5);
            firstAfter = Views.ExplorePanel.Places.Cards[0].PlaceName.GetText();
        }

        Assert.That(firstAfter, Is.Not.EqualTo(firstBefore),
            $"Selecting the '{category}' category should reload the results grid with different leading places");
        Reporter.Log($"Filter applied: leading card changed from '{firstBefore}' to '{firstAfter}'");

        // Restore the ALL chip (tap — see above) — the section keeps its state across
        // panel close/reopen.
        Views.ExplorePanel.Places.CategoryButtons[0].Tap();
        Wait(1);
        Reporter.Log("Category filter reset to ALL");

        Views.ExplorePanel.Close();
    }

    [Test]
    public void TestOpenPlacesFilterDropdown()
    {
        OpenPlaces();

        Views.ExplorePanel.Places.FilterSortButton.Click();
        Views.ExplorePanel.Places.FiltersContent.WaitFor(10);
        Assert.That(Views.ExplorePanel.Places.TrendingToggle.IsPresent(), Is.True,
            "Filter dropdown should contain the Trending sort toggle");
        Assert.That(Views.ExplorePanel.Places.MostActiveToggle.IsPresent(), Is.True,
            "Filter dropdown should contain the Most Active sort toggle");
        Assert.That(Views.ExplorePanel.Places.CompatibleOnlyToggle.IsPresent(), Is.True,
            "Filter dropdown should contain the Compatible Only view toggle");
        Reporter.Log("Filter & Sort dropdown opened with all controls");

        Views.ExplorePanel.Places.FilterSortButton.Click();
        Views.ExplorePanel.Places.FiltersContent.WaitForGone(10);
        Reporter.Log("Filter & Sort dropdown closed");

        Views.ExplorePanel.Close();
    }

    /// <summary>
    /// Opens the Places section via the keyboard shortcut. Deliberately NOT the sidebar
    /// button: the sidebar controller's open-section state goes stale when the panel closes
    /// abnormally (Escape, teleport), after which the section's own button consumes clicks
    /// as "close" no-ops — the shortcut is immune. The sidebar-click entry path is covered
    /// by ExplorePanelTests/ShortcutsTests; depth tests just need the panel open reliably.
    /// </summary>
    private void OpenPlaces()
    {
        ClickUntil(() => PressKey(AltKeyCode.Z),
                   () => Views.ExplorePanel.Places.IsPresent());
        Views.ExplorePanel.Places.WaitFor();
    }
}
