namespace ExplorerAutomation.Tests.Tests;

// Depth coverage for the explore panel's Places section (the open-from-sidebar smoke test
// lives in ExplorePanelTests). The in-world Order band 10-19 is full, so this fixture
// shares Order 11 with ExplorePanelTests (duplicate Orders have precedent at 16 and 19).
[AllureSuite("Places Tests")]
[Category("InWorld")]
[Order(11)]
public class PlacesTests : BaseTest
{
    [Test]
    public void TestSwitchPlacesTabs()
    {
        Views.MainMenu.PlacesButton.Click();
        Views.ExplorePanel.Places.WaitFor();

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

        // Explore (the default tab) hides the counter row again.
        Views.ExplorePanel.Places.ExploreTabButton.Click();
        Views.ExplorePanel.Places.ResultsCounter.WaitForGone();
        Reporter.Log("Explore tab restored — counter hidden");

        Views.ExplorePanel.Close();
    }

    [Test]
    public void TestSearchPlaces()
    {
        Views.MainMenu.PlacesButton.Click();
        Views.ExplorePanel.Places.WaitFor();

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
        Views.MainMenu.PlacesButton.Click();
        Views.ExplorePanel.Places.WaitFor();

        var placeName = Views.ExplorePanel.Places.Cards[0].PlaceName.GetText();
        // Click the thumbnail, not the card root — the hover overlay puts JUMP IN and the
        // like/heart/home buttons at the card's center (see PlaceCard doc comment).
        Views.ExplorePanel.Places.Cards[0].Thumbnail.Click();

        Views.ExplorePanel.Places.PlaceDetail.WaitFor();
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
        Views.MainMenu.PlacesButton.Click();
        Views.ExplorePanel.Places.WaitFor();

        // Chip order: ALL, SOCIAL, MUSIC, ART, GAME, FASHION, EDUCATION, SHOP, SPORTS, BUSINESS.
        var category = Views.ExplorePanel.Places.CategoryLabels[3].GetText();
        Views.ExplorePanel.Places.CategoryButtons[3].Click();
        Wait(2); // results grid reloads after selecting a category
        Reporter.Log($"Selected category chip '{category}'");

        // The chips carry no readable selected-state, so verify the filter end-to-end:
        // the first filtered place must list the selected category among its detail tags.
        // Thumbnail click, not root click — see PlaceCard doc comment.
        Views.ExplorePanel.Places.Cards[0].Thumbnail.Click();
        Views.ExplorePanel.Places.PlaceDetail.WaitFor();

        var tags = Views.ExplorePanel.Places.PlaceDetail.GetCategoryTags();
        Assert.That(tags, Has.Some.EqualTo(category).IgnoreCase,
            $"A place filtered by '{category}' should carry that category tag in its detail view");

        Views.ExplorePanel.Places.PlaceDetail.CloseButton.Click();
        Views.ExplorePanel.Places.PlaceDetail.WaitForGone();

        // Restore the ALL chip — the section keeps its state across panel close/reopen.
        Views.ExplorePanel.Places.CategoryButtons[0].Click();
        Wait(1);
        Reporter.Log("Category filter reset to ALL");

        Views.ExplorePanel.Close();
    }

    [Test]
    public void TestOpenPlacesFilterDropdown()
    {
        Views.MainMenu.PlacesButton.Click();
        Views.ExplorePanel.Places.WaitFor();

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
}
