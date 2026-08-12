namespace ExplorerAutomation.Tests.Views.ExplorePanelSections;

/// <summary>
/// Section view for the Places tab within the explore panel, displaying discoverable Decentraland locations.
/// </summary>
public class ExplorePanelPlacesView : BaseSection
{
    #region Elements

    private const int CARD_COUNT     = 15;
    private const int CATEGORY_COUNT = 10;
    // Only ever spent re-reading a thumbnail a presence probe just resolved.
    private const double THUMBNAIL_TIMEOUT = 5D;

    private const string CARDS_CONTAINER =
        "//Places/Content/PlacesResults/ResultsContainer/LoadedState/ResultsScrollView/Viewport/ResultsContainer";

    public readonly Clickable ExploreTabButton   = new(By.PATH, "//Places/Header/TabSelector/Explore");
    public readonly Clickable RecentTabButton    = new(By.PATH, "//Places/Header/TabSelector/Recent");
    public readonly Clickable FavoritesTabButton = new(By.PATH, "//Places/Header/TabSelector/Favorites");
    public readonly Clickable MyPlacesTabButton  = new(By.PATH, "//Places/Header/TabSelector/MyPlaces");
    public readonly Clickable FilterSortButton   = new(By.PATH, "//Places/Header/Filters/Places_FilterSelector/FilterButton");
    public readonly Writable  SearchBar          = new(By.PATH, "//Places/Header/Filters/SearchBar");
    public readonly Locatable LoadedState        = new(By.PATH, "//Places/Content/PlacesResults/ResultsContainer/LoadedState");
    // The counter row above the results grid. Disabled on the default Explore tab (with the
    // ALL category), enabled on the Recent/Favorites/MyPlaces tabs ("Recent (7)"), and while
    // a search is active ("Results for 'Genesis Plaza' (24)").
    public readonly Readable  ResultsCounter     = new(By.PATH, "//PlacesSection//ResultsCounterContainer/ResultsCounter");
    // Only enabled while the search bar holds text; clicking it clears the search and
    // returns to the Explore tab.
    public readonly Clickable ClearSearchButton  = new(By.PATH, "//PlacesSection//SearchBar//ClearSearchButton");
    // "Filter & Sort" dropdown content. Disabled until FilterSortButton is clicked.
    public readonly Locatable FiltersContent       = new(By.PATH, "//PlacesSection//FiltersContent");
    public readonly Clickable TrendingToggle       = new(By.PATH, "//PlacesSection//FiltersContent//Trending");
    public readonly Clickable MostActiveToggle     = new(By.PATH, "//PlacesSection//FiltersContent//MostActive");
    // Labelled "Compatible Only" in the UI; GameObject name is Recommended.
    public readonly Clickable CompatibleOnlyToggle = new(By.PATH, "//PlacesSection//FiltersContent//Recommended");

    public Clickable[] CategoryButtons { get; }
    // Displayed label of each category chip (index-aligned with CategoryButtons):
    // ALL, SOCIAL, MUSIC, ART, GAME, FASHION, EDUCATION, SHOP, SPORTS, BUSINESS.
    public Readable[] CategoryLabels { get; }
    public PlaceCard[] Cards { get; }

    #endregion

    #region Setup

    public ExplorePanelPlacesView() : base(new(By.NAME, "PlacesSection"))
    {
        CategoryButtons = new Clickable[CATEGORY_COUNT];
        CategoryLabels  = new Readable[CATEGORY_COUNT];
        for (var i = 0; i < CATEGORY_COUNT; i++)
        {
            CategoryButtons[i] = new(By.PATH, $"//Places/Content/CategoriesContainer/CategoryButton(Clone)[{i}]");
            CategoryLabels[i]  = new(By.PATH, $"//Places/Content/CategoriesContainer/CategoryButton(Clone)[{i}]//Text");
        }

        Cards = new PlaceCard[CARD_COUNT];
        for (var i = 0; i < CARD_COUNT; i++)
            Cards[i] = new PlaceCard($"{CARDS_CONTAINER}/PlaceCard(Clone)[{i}]");
    }

    #endregion

    #region Views

    public PlaceDetailView PlaceDetail { get; } = new();

    #endregion

    #region Helper methods

    /// <summary>
    /// Waits until the results grid accepts presses. The skeleton only hands the loaded
    /// CanvasGroup its raycasts back when the fade-in completes, so a press before that is
    /// rejected by the group rather than by the card.
    /// </summary>
    [AllureStep("Wait for the places results grid to accept presses")]
    public void WaitForResultsInteractive(double timeout = SlowChassis.SETTLE_TIMEOUT)
    {
        LoadedState.WaitFor(timeout, verificationShot: false)
                   .WaitForComponentProperty("UnityEngine.CanvasGroup", "blocksRaycasts", true,
                        "UnityEngine.UIModule", timeout: timeout);
        Reporter.Log("Places results grid accepts presses");
    }

    /// <summary>
    /// Returns the card whose thumbnail sits left-top-most on screen — the only safe click
    /// target, since the recycling grid keeps a live row parked outside the viewport, and a
    /// press dispatched there does nothing at all. Top-left also keeps the press clear of the
    /// Nearby Voice Chat tip, which sits bottom-left for the whole of a CI run.
    /// </summary>
    [AllureStep("Find the left-top-most visible place card")]
    public PlaceCard FindTopLeftVisibleCard()
    {
        PlaceCard topLeft = null;
        var topLeftY = int.MaxValue;
        var topLeftX = int.MaxValue;

        foreach (var card in Cards)
        {
            // The thumbnail is what gets clicked, so the thumbnail is what has to be on screen.
            // Shot-suppressed probes — target selection, not a test verification.
            if (!card.Thumbnail.IsPresent(verificationShot: false))
                continue;

            var thumbnail = card.Thumbnail.WaitFor(THUMBNAIL_TIMEOUT, verificationShot: false);
            if (!IsOnScreen(thumbnail))
                continue;

            // Grid rows share a mobileY exactly, so an equal row falls to the leftmost column.
            if (thumbnail.mobileY > topLeftY || (thumbnail.mobileY == topLeftY && thumbnail.x >= topLeftX))
                continue;

            topLeft  = card;
            topLeftY = thumbnail.mobileY;
            topLeftX = thumbnail.x;
        }

        if (topLeft is null)
            throw new AssertionException(
                "No place card thumbnail is on screen — the results grid is empty or never finished loading");

        Reporter.Log($"Picked the place card whose thumbnail is at x={topLeftX}, mobileY={topLeftY}");
        return topLeft;
    }

    // Centre inside both edges — y is bottom-origin, mobileY top-origin, so no screen size needed.
    private static bool IsOnScreen(AltObject thumbnail) => thumbnail.y > 0 && thumbnail.mobileY > 0;

    #endregion

    #region Sub views

    /// <summary>
    /// Clickable view representing a single place card in the places grid,
    /// with interaction buttons (like, dislike, favorite, home, share) and a jump-in button.
    /// WARNING: do not click the card root to open the place detail — pointer-enter reveals
    /// a hover overlay whose Interactions row and JUMP IN button sit at the card's center,
    /// so a root click can favorite/teleport instead. Click <see cref="Thumbnail"/> (top of
    /// the card, never covered by the overlay) to open the detail popup.
    /// Also note the grid is a recycling LoopGridView: the hierarchy index names a slot, not a
    /// position, and one live row is always parked outside the viewport. Click via
    /// <see cref="FindTopLeftVisibleCard"/>; element paths within one card always refer to that
    /// same card's content.
    /// </summary>
    public class PlaceCard : BaseClickableView
    {
        #region Elements

        public readonly Clickable Thumbnail;
        public readonly Readable  PlaceName;
        public readonly Readable  Creator;
        public readonly Readable  LikeRateText;
        public readonly Readable  CoordsText;
        public readonly Readable  LivePlayerCount;
        public readonly Locatable FeaturedTag;
        public readonly Clickable LikeButton;
        public readonly Clickable DislikeButton;
        public readonly Clickable HeartButton;
        public readonly Clickable HomeButton;
        public readonly Clickable ShareButton;
        public readonly Clickable JumpInButton;

        #endregion

        #region Setup

        public PlaceCard(string basePath) : base(new(By.PATH, basePath))
        {
            Thumbnail       = new(By.PATH, $"{basePath}/Header/ImageWithSkeletonAnimation");
            PlaceName       = new(By.PATH, $"{basePath}/Footer/Texts/PlaceName");
            Creator         = new(By.PATH, $"{basePath}/Footer/Texts/Creator");
            LikeRateText    = new(By.PATH, $"{basePath}/Footer/Texts/LikeRateAndCoordsRow/LikeRate/LikeRateText");
            CoordsText      = new(By.PATH, $"{basePath}/Footer/Texts/LikeRateAndCoordsRow/PlaceCoords/PlaceCoordsText");
            LivePlayerCount = new(By.PATH, $"{basePath}/Header/SocialInfoContainer/OnlineCounter/LiveText");
            FeaturedTag     = new(By.PATH, $"{basePath}/Header/FeaturedTag");
            LikeButton      = new(By.PATH, $"{basePath}/Footer/ButtonsContainer/Interactions/Like");
            DislikeButton   = new(By.PATH, $"{basePath}/Footer/ButtonsContainer/Interactions/Dislike");
            HeartButton     = new(By.PATH, $"{basePath}/Footer/ButtonsContainer/Interactions/Heart");
            HomeButton      = new(By.PATH, $"{basePath}/Footer/ButtonsContainer/Interactions/Home");
            ShareButton     = new(By.PATH, $"{basePath}/Footer/ButtonsContainer/Interactions/Share");
            JumpInButton    = new(By.PATH, $"{basePath}/Footer/ButtonsContainer/JumpIntoWorld");
        }

        #endregion
    }

    #endregion
}
