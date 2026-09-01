namespace ExplorerAutomation.Tests.Views.ExplorePanelSections;

/// <summary>
/// Section view for the Communities tab within the explore panel. A left column holds the
/// Create button, the Invites &amp; Requests entry and the My Communities strip; the right
/// side shows the Browse Communities grid (which the Invites &amp; Requests results view
/// replaces in-place when opened).
/// </summary>
public class ExplorePanelCommunitiesView : BaseSection
{
    #region Elements

    private const int CARD_COUNT = 15;

    public readonly Writable  SearchBar = new(By.PATH, "//CommunitiesSection//Header/SearchBar");
    // The outer CreateCommunityButton/InvitesAndRequestsButton objects are containers that
    // wrap an inner Button of the same name — target the inner one for reliable clicks.
    public readonly Clickable CreateCommunityButton    = new(By.PATH, "//CommunitiesSection//CreateCommunityButton/CreateCommunityButton");
    public readonly Clickable InvitesAndRequestsButton = new(By.PATH, "//CommunitiesSection//InvitesAndRequestsButton/InvitesAndRequestsButton");

    public readonly Locatable MyCommunitiesSection = new(By.PATH, "//CommunitiesSection//CommunitiesBrowser_MyCommunitiesSection");
    public readonly Readable  MyCommunitiesTitle   = new(By.PATH, "//CommunitiesSection//MyCommunitiesTitle");

    // Browse grid header. Title reads "Browse Communities" by default and
    // "Results for '<query>'" while a search is active; count reads "(179)".
    public readonly Readable  BrowseResultsTitle = new(By.PATH, "//CommunitiesSection//CommunitiesGridView//ResultsTitle");
    public readonly Readable  BrowseResultsCount = new(By.PATH, "//CommunitiesSection//CommunitiesGridView//ResultsCount");
    // Only enabled while a search is active; clicking it clears the search.
    public readonly Clickable BrowseBackButton   = new(By.PATH, "//CommunitiesSection//CommunitiesGridView//BackButton");

    public CommunityResultCard[] Cards { get; }

    #endregion

    #region Setup

    public ExplorePanelCommunitiesView() : base(new(By.NAME, "CommunitiesSection"))
    {
        Cards = new CommunityResultCard[CARD_COUNT];
        for (var i = 0; i < CARD_COUNT; i++)
            Cards[i] = new CommunityResultCard(
                $"//CommunitiesSection//CommunitiesGridView//ResultsContainer/CommunityResultCard(Clone)[{i}]");
    }

    #endregion

    #region Views

    public InvitesAndRequestsView InvitesAndRequests { get; } = new();
    public CommunityDetailView    CommunityDetail    { get; } = new();

    #endregion

    #region Sub views

    /// <summary>
    /// Clickable view for a single community card in the browse grid.
    /// WARNING: like the Places grid, the browse grid pools its card instances — after the
    /// section refreshes its content (including on reopen) the hierarchy index no longer
    /// matches the visual order, and a pooled card can sit below the viewport fold where
    /// clicks hit nothing. Open the detail by clicking <see cref="Thumbnail"/> (the header
    /// image, clear of the Join button and the description hover tooltip) and verify the
    /// detail actually opened before asserting on it.
    /// </summary>
    public class CommunityResultCard : BaseClickableView
    {
        #region Elements

        public readonly Clickable Thumbnail;
        public readonly Readable Title;
        public readonly Readable OwnerName;
        public readonly Readable MembersCountText;
        public readonly Readable PrivacyText;
        // Swap pair: JoinButton (or "Request to join" for private communities) before
        // joining, ViewButton once a member — only one present at a time.
        public readonly Clickable JoinButton;
        public readonly Clickable ViewButton;

        #endregion

        #region Setup

        public CommunityResultCard(string basePath) : base(new(By.PATH, basePath))
        {
            Thumbnail        = new(By.PATH, $"{basePath}/Header");
            Title            = new(By.PATH, $"{basePath}/Footer/Title");
            OwnerName        = new(By.PATH, $"{basePath}/Footer/OwnerName");
            MembersCountText = new(By.PATH, $"{basePath}/Footer/SecondLineContainer/MembersCountText");
            PrivacyText      = new(By.PATH, $"{basePath}/Footer/SecondLineContainer/PrivacyText");
            JoinButton       = new(By.PATH, $"{basePath}//JoinButton");
            ViewButton       = new(By.PATH, $"{basePath}//ViewButton");
        }

        #endregion
    }

    /// <summary>
    /// Sub-view for the Invites &amp; Requests results list that replaces the browse grid
    /// (the section root stays disabled until InvitesAndRequestsButton is clicked).
    /// </summary>
    public class InvitesAndRequestsView() : BaseView(new(By.PATH, "//CommunitiesSection//InvitesAndRequestsSection"))
    {
        #region Elements

        public readonly Readable  Title      = new(By.PATH, "//CommunitiesSection//InvitesAndRequestsSection//ResultsTitle");
        public readonly Clickable BackButton = new(By.PATH, "//CommunitiesSection//InvitesAndRequestsSection//BackButton");

        #endregion
    }

    #endregion
}
