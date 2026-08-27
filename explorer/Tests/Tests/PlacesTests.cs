namespace ExplorerAutomation.Tests.Tests;

// Depth coverage for the explore panel's Places section (the open-from-sidebar smoke test
// lives in ExplorePanelTests). The in-world Order band 10-19 is full, so this fixture
// shares Order 11 with ExplorePanelTests (duplicate Orders have precedent at 16 and 19).
[AllureSuite("Places Tests")]
[Category("InWorld")]
[Order(11)]
public class PlacesTests : BaseTest
{
    // Grace given to a press before it is retried — long enough that a working-but-slow open is
    // not second-guessed, short enough to leave room for the authoritative wait.
    private const double DETAIL_OPEN_GRACE = 15;

    [Test]
    public void TestSwitchPlacesTabs()
    {
        OpenPlaces();

        Views.ExplorePanel.Places.RecentTabButton.Click(settleMs: 0);
        // The counter enables before its text is refreshed — poll for the actual text
        // instead of guessing a fixed settle time.
        var recentCounterText = Views.ExplorePanel.Places.ResultsCounter.WaitForText(text => text.StartsWith("Recent"));
        Assert.That(recentCounterText, Does.StartWith("Recent"),
            "Recent tab should show the 'Recent (N)' results counter");
        Reporter.Log("Recent tab opened");

        Views.ExplorePanel.Places.FavoritesTabButton.Click(settleMs: 0);
        var favoritesCounterText = Views.ExplorePanel.Places.ResultsCounter.WaitForText(text => text.StartsWith("Favorites"));
        Assert.That(favoritesCounterText, Does.StartWith("Favorites"),
            "Favorites tab should show the 'Favorites (N)' results counter");
        Reporter.Log("Favorites tab opened");

        Views.ExplorePanel.Places.MyPlacesTabButton.Click(settleMs: 0);
        var myPlacesCounterText = Views.ExplorePanel.Places.ResultsCounter.WaitForText(text => text.StartsWith("My Places"));
        Assert.That(myPlacesCounterText, Does.StartWith("My Places"),
            "My Places tab should show the 'My Places (N)' results counter");
        Reporter.Log("My Places tab opened");

        Views.ExplorePanel.Places.ExploreTabButton.Click(settleMs: 0);
        Views.ExplorePanel.Places.ResultsCounter.WaitForGone();
        Reporter.Log("Explore tab restored — counter hidden");

        Views.ExplorePanel.Close();
    }

    [Test]
    public void TestSearchPlaces()
    {
        OpenPlaces();

        Views.ExplorePanel.Places.SearchBar.SetText("Genesis Plaza");
        // Poll for the results counter to reflect the query instead of guessing how long
        // the remote search takes to resolve.
        var counterText = Views.ExplorePanel.Places.ResultsCounter.WaitForText(
            text => text.StartsWith("Results for 'Genesis Plaza'"));

        Assert.That(counterText,
            Does.StartWith("Results for 'Genesis Plaza'"),
            "Search should show a 'Results for ...' counter");
        Assert.That(Views.ExplorePanel.Places.Cards[0].PlaceName.GetText(),
            Does.Contain("Genesis Plaza"),
            "First search result should match the query");
        Reporter.Log("Search for 'Genesis Plaza' returned matching results");

        Views.ExplorePanel.Places.ClearSearchButton.Click(settleMs: 0);
        Views.ExplorePanel.Places.ResultsCounter.WaitForGone();
        Reporter.Log("Search cleared");

        Views.ExplorePanel.Close();
    }

    [Test]
    public void TestOpenPlaceDetail()
    {
        OpenPlaces();
        Views.ExplorePanel.Places.WaitForResultsInteractive();

        // Card resolution and name capture happen inside the retry: the grid re-binds and
        // re-parks its slots while results stream in, so a card resolved before the click is
        // not necessarily the card the click lands on.
        var placeName = string.Empty;
        ClickUntil(() =>
        {
            var card = Views.ExplorePanel.Places.FindTopLeftVisibleCard();
            placeName = card.PlaceName.GetText();

            // Click the thumbnail, not the card root — the hover overlay puts JUMP IN and the
            // like/heart/home buttons at the card's centre (see PlaceCard doc comment), and the
            // press bubbles to the card's own Button either way. Do NOT add a card-root press as
            // a fallback: by then the pointer sits on the card, so the overlay is up and the
            // root's centre is where JUMP IN and the like/home buttons are. A press there
            // teleports the player or mutates a favourite on the shared account, corrupting the
            // rest of this single-session ordered suite.
            card.Thumbnail.Click();
        }, DetailIsOpen, attempts: 2, timeoutPerAttempt: DETAIL_OPEN_GRACE);

        Views.ExplorePanel.Places.PlaceDetail.WaitFor(SlowChassis.SETTLE_TIMEOUT);
        Assert.That(Views.ExplorePanel.Places.PlaceDetail.PlaceTitle.GetText(), Is.EqualTo(placeName),
            "Place detail title should match the clicked card");
        Reporter.Log($"Place detail opened for '{placeName}'");

        Views.ExplorePanel.Places.PlaceDetail.CloseButton.Click(settleMs: 0);
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
            // Shot-suppressed poll read: one shot below, on the value the assert compares.
            firstAfter = Views.ExplorePanel.Places.Cards[0].PlaceName.GetText(20D, verificationShot: false);
        }

        Reporter.TakeVerificationShot("text_PlaceName");
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

        Views.ExplorePanel.Places.FilterSortButton.Click(settleMs: 0);
        Views.ExplorePanel.Places.FiltersContent.WaitFor(10);
        Assert.That(Views.ExplorePanel.Places.TrendingToggle.IsPresent(), Is.True,
            "Filter dropdown should contain the Trending sort toggle");
        Assert.That(Views.ExplorePanel.Places.MostActiveToggle.IsPresent(), Is.True,
            "Filter dropdown should contain the Most Active sort toggle");
        Assert.That(Views.ExplorePanel.Places.CompatibleOnlyToggle.IsPresent(), Is.True,
            "Filter dropdown should contain the Compatible Only view toggle");
        Reporter.Log("Filter & Sort dropdown opened with all controls");

        Views.ExplorePanel.Places.FilterSortButton.Click(settleMs: 500);
        Views.ExplorePanel.Places.FiltersContent.WaitForGone(20);
        Reporter.Log("Filter & Sort dropdown closed");

        Views.ExplorePanel.Close();
    }

    private bool DetailIsOpen() =>
        Views.ExplorePanel.Places.PlaceDetail.IsPresent(verificationShot: false);

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
