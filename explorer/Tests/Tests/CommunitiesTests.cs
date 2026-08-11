namespace ExplorerAutomation.Tests.Tests;

// Depth coverage for the explore panel's Communities section (the open-from-sidebar smoke
// test lives in ExplorePanelTests). Read-only: no community is created, joined or left.
// The in-world Order band 10-19 is full, so this fixture shares Order 11 with
// ExplorePanelTests (duplicate Orders have precedent at 16 and 19).
[AllureSuite("Communities Tests")]
[Category("InWorld")]
[Order(11)]
public class CommunitiesTests : BaseTest
{
    [Test]
    public void TestCommunitiesPanelContent()
    {
        OpenCommunities();

        Assert.That(Views.ExplorePanel.Communities.CreateCommunityButton.IsPresent(), Is.True,
            "Left column should offer the CREATE A COMMUNITY button");
        Assert.That(Views.ExplorePanel.Communities.InvitesAndRequestsButton.IsPresent(), Is.True,
            "Left column should offer the Invites & Requests entry");
        Assert.That(Views.ExplorePanel.Communities.MyCommunitiesTitle.GetText(), Is.EqualTo("My Communities"),
            "Left column should contain the My Communities strip");
        Assert.That(Views.ExplorePanel.Communities.BrowseResultsTitle.GetText(), Is.EqualTo("Browse Communities"),
            "Right side should show the Browse Communities grid");
        Assert.That(Views.ExplorePanel.Communities.BrowseResultsCount.GetText(), Does.Match(@"^\(\d+\)$"),
            "Browse Communities should show a total count");

        var firstCommunity = Views.ExplorePanel.Communities.Cards[0].Title.GetText();
        Assert.That(firstCommunity, Is.Not.Empty, "The browse grid should contain community cards");
        Reporter.Log($"Communities panel content verified; first community: '{firstCommunity}'");

        Views.ExplorePanel.Close();
    }

    [Test]
    public void TestOpenInvitesAndRequests()
    {
        OpenCommunities();

        Views.ExplorePanel.Communities.InvitesAndRequestsButton.Click();
        Views.ExplorePanel.Communities.InvitesAndRequests.WaitFor();
        Assert.That(Views.ExplorePanel.Communities.InvitesAndRequests.Title.GetText(),
            Is.EqualTo("Invites & Requests"),
            "Invites & Requests view should replace the browse grid");
        Reporter.Log("Invites & Requests view opened");

        Views.ExplorePanel.Communities.InvitesAndRequests.BackButton.Click();
        Views.ExplorePanel.Communities.InvitesAndRequests.WaitForGone();
        Assert.That(Views.ExplorePanel.Communities.BrowseResultsTitle.GetText(), Is.EqualTo("Browse Communities"),
            "Back button should restore the Browse Communities grid");
        Reporter.Log("Returned to the browse grid");

        Views.ExplorePanel.Close();
    }

    [Test]
    public void TestSearchCommunities()
    {
        OpenCommunities();

        Views.ExplorePanel.Communities.SearchBar.SetText("Decentraland");
        // Poll for the grid title to reflect the query instead of guessing how long the
        // remote search takes to resolve.
        var titleText = Views.ExplorePanel.Communities.BrowseResultsTitle.WaitForText(
            text => text == "Results for 'Decentraland'");

        Assert.That(titleText,
            Is.EqualTo("Results for 'Decentraland'"),
            "Search should retitle the grid with the query");
        Assert.That(Views.ExplorePanel.Communities.BrowseResultsCount.GetText(), Does.Match(@"^\(\d+\)$"),
            "Search should show a result count");
        Assert.That(Views.ExplorePanel.Communities.Cards[0].Title.GetText(), Is.Not.Empty,
            "Search should return community cards");
        Reporter.Log("Community search for 'Decentraland' returned results");

        Views.ExplorePanel.Communities.BrowseBackButton.Click();
        var restoredTitleText = Views.ExplorePanel.Communities.BrowseResultsTitle.WaitForText(text => text == "Browse Communities");
        Assert.That(restoredTitleText, Is.EqualTo("Browse Communities"),
            "Back button should clear the search");
        Reporter.Log("Search cleared");

        Views.ExplorePanel.Close();
    }

    [Test]
    public void TestOpenCommunityDetail()
    {
        OpenCommunities();

        // The pooled grid can leave any given card index below the viewport fold (see
        // CommunityResultCard doc comment), so try the first few cards until a click on a
        // card's header image actually opens the detail popup.
        string communityName = null;
        for (var i = 0; i < 5 && communityName == null; i++)
        {
            var card = Views.ExplorePanel.Communities.Cards[i];
            var cardTitle = card.Title.GetText();
            card.Thumbnail.Click();

            if (WaitUntil(() => Views.ExplorePanel.Communities.CommunityDetail.IsPresent(verificationShot: false), 2))
                communityName = cardTitle;
            else
                Reporter.Log($"Card {i} ('{cardTitle}') click did not open the detail — likely below the fold, trying next card");
        }

        Assert.That(communityName, Is.Not.Null,
            "Clicking a community card's header should open the community detail popup");

        Views.ExplorePanel.Communities.CommunityDetail.WaitFor();
        // Header content loads asynchronously — poll for the name instead of guessing.
        var detailNameText = Views.ExplorePanel.Communities.CommunityDetail.CommunityName.WaitForText(
            text => text == communityName);
        Assert.That(detailNameText,
            Is.EqualTo(communityName),
            "Community detail should show the clicked community's name");
        // Public communities expose content section tabs; private ones show an access
        // restriction notice instead — either proves the detail content area rendered.
        Assert.That(
            Views.ExplorePanel.Communities.CommunityDetail.AnnouncementsSectionButton.IsPresent()
            || Views.ExplorePanel.Communities.CommunityDetail.PrivateAccessRestriction.IsPresent(),
            Is.True,
            "Community detail should show section tabs or the private-access notice");
        Reporter.Log($"Community detail opened for '{communityName}'");

        Views.ExplorePanel.Communities.CommunityDetail.CloseButton.Click();
        Views.ExplorePanel.Communities.CommunityDetail.WaitForGone();

        Views.ExplorePanel.Close();
    }

    /// <summary>
    /// Opens the Communities section via the keyboard shortcut. Deliberately NOT the
    /// sidebar button — see PlacesTests.OpenPlaces for the stale open-section-state
    /// rationale. The sidebar-click entry path is covered by ExplorePanelTests/ShortcutsTests.
    /// </summary>
    private void OpenCommunities()
    {
        ClickUntil(() => PressKey(AltKeyCode.O, delay: 0),
                   () => Views.ExplorePanel.Communities.IsPresent());
        Views.ExplorePanel.Communities.WaitFor();
    }
}
