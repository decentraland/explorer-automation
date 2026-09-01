using ExplorerAutomation.Tests.Views.ExplorePanelSections;

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

        Views.ExplorePanel.Communities.InvitesAndRequestsButton.Click(settleMs: 0);
        Views.ExplorePanel.Communities.InvitesAndRequests.WaitFor();
        Assert.That(Views.ExplorePanel.Communities.InvitesAndRequests.Title.GetText(),
            Is.EqualTo("Invites & Requests"),
            "Invites & Requests view should replace the browse grid");
        Reporter.Log("Invites & Requests view opened");

        Views.ExplorePanel.Communities.InvitesAndRequests.BackButton.Click(settleMs: 0);
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
        // The count lands after the title, so it needs its own wait — reading it off the back
        // of the title wait is a race the search loses whenever it resolves quickly.
        var countText = Views.ExplorePanel.Communities.BrowseResultsCount.WaitForText(
            text => text.StartsWith('(') && text.EndsWith(')'));

        Assert.That(countText, Does.Match(@"^\(\d+\)$"),
            "Search should show a result count");
        Assert.That(Views.ExplorePanel.Communities.Cards[0].Title.GetText(), Is.Not.Empty,
            "Search should return community cards");
        Reporter.Log("Community search for 'Decentraland' returned results");

        Views.ExplorePanel.Communities.BrowseBackButton.Click(settleMs: 0);
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
            // Shot-suppressed read: naming the candidate is card selection, retried per card.
            // One shot below, on the card whose click actually opened the detail.
            var cardTitle = card.Title.GetText(20D, verificationShot: false);
            card.Thumbnail.Click();

            if (WaitUntil(() => Views.ExplorePanel.Communities.CommunityDetail.IsPresent(verificationShot: false), 2))
            {
                communityName = cardTitle;
                Reporter.TakeVerificationShot($"opened_CommunityCard_{i}");
            }
            else
            {
                Reporter.Log($"Card {i} ('{cardTitle}') click did not open the detail — likely below the fold, trying next card");
            }
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

        Views.ExplorePanel.Communities.CommunityDetail.CloseButton.Click(settleMs: 0);
        Views.ExplorePanel.Communities.CommunityDetail.WaitForGone();

        Views.ExplorePanel.Close();
    }

    [Test]
    public void TestJoinAndLeaveCommunity()
    {
        OpenCommunities();

        // Search for a specific, stable community instead of relying on the pooled grid's
        // default order/index (see explorer/CLAUDE.md "Pooled Lists") — Decentraland
        // Foundation is the official account and always the top result for this query.
        Views.ExplorePanel.Communities.SearchBar.SetText("Decentraland Foundation");
        Views.ExplorePanel.Communities.BrowseResultsTitle.WaitForText(
            text => text == "Results for 'Decentraland Foundation'");

        var card = Views.ExplorePanel.Communities.Cards[0];
        var communityName = card.Title.GetText();

        // Idempotent precondition: if an earlier aborted run left this joined, leave first so
        // the join half below has a deterministic starting state.
        if (card.ViewButton.IsPresent())
        {
            Reporter.Log($"'{communityName}' already joined from an earlier run — leaving first");
            LeaveFromGrid(card);
            // The grid card takes a beat to flip back from View to Join after the detail
            // popup closes — same async gap as the header state noted below.
            WaitUntil(() => card.JoinButton.IsPresent(verificationShot: false));
        }

        card.JoinButton.Click();
        WaitUntil(() => card.ViewButton.IsPresent(verificationShot: false));
        Assert.That(card.ViewButton.IsPresent(), Is.True,
            $"'{communityName}' card should switch from Join to View after joining");
        Reporter.Log($"Joined '{communityName}' from the browse grid");

        card.ViewButton.Click();
        Views.ExplorePanel.Communities.CommunityDetail.WaitFor();
        // Header membership state resolves a beat after the popup itself appears (same shape
        // as CommunityName loading asynchronously below) — wait for it rather than asserting
        // on the frame the popup opened.
        Views.ExplorePanel.Communities.CommunityDetail.JoinedButton.WaitFor(10);
        Reporter.Log("Community detail confirms joined state");

        Views.ExplorePanel.Communities.CommunityDetail.JoinedButton.Click();
        Views.ConfirmationDialog.WaitFor();
        Views.ConfirmationDialog.YesButton.Click();
        Views.ConfirmationDialog.WaitForGone();
        WaitUntil(() => Views.ExplorePanel.Communities.CommunityDetail.JoinButton.IsPresent(verificationShot: false));
        Assert.That(Views.ExplorePanel.Communities.CommunityDetail.JoinButton.IsPresent(), Is.True,
            "Community detail header should revert to Join after confirming leave");
        Reporter.Log($"Left '{communityName}' via the detail header");

        Views.ExplorePanel.Communities.CommunityDetail.CloseButton.Click();
        Views.ExplorePanel.Communities.CommunityDetail.WaitForGone();

        Views.ExplorePanel.Communities.BrowseBackButton.Click();
        Views.ExplorePanel.Close();
    }

    /// <summary>
    /// Leaves a community from the browse grid's own View state, for the precondition
    /// cleanup in <see cref="TestJoinAndLeaveCommunity"/>.
    /// </summary>
    private void LeaveFromGrid(ExplorePanelCommunitiesView.CommunityResultCard card)
    {
        card.ViewButton.Click();
        Views.ExplorePanel.Communities.CommunityDetail.WaitFor();
        Views.ExplorePanel.Communities.CommunityDetail.JoinedButton.Click();
        Views.ConfirmationDialog.WaitFor();
        Views.ConfirmationDialog.YesButton.Click();
        Views.ConfirmationDialog.WaitForGone();
        WaitUntil(() => Views.ExplorePanel.Communities.CommunityDetail.JoinButton.IsPresent(verificationShot: false));
        Views.ExplorePanel.Communities.CommunityDetail.CloseButton.Click();
        Views.ExplorePanel.Communities.CommunityDetail.WaitForGone();
    }

    /// <summary>
    /// Opens the Communities section via the keyboard shortcut. Deliberately NOT the
    /// sidebar button — see PlacesTests.OpenPlaces for the stale open-section-state
    /// rationale. The sidebar-click entry path is covered by ExplorePanelTests/ShortcutsTests.
    /// </summary>
    private void OpenCommunities()
    {
        ClickUntil(() => PressKey(AltKeyCode.O),
                   () => Views.ExplorePanel.Communities.IsPresent());
        Views.ExplorePanel.Communities.WaitFor();
    }
}
