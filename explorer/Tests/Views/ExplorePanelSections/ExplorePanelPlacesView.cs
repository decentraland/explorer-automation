namespace ExplorerAutomation.Tests.Views.ExplorePanelSections;

/// <summary>
/// Section view for the Places tab within the explore panel, displaying discoverable Decentraland locations.
/// </summary>
public class ExplorePanelPlacesView : BaseSection
{
    #region Elements

    private const int CARD_COUNT     = 15;
    private const int CATEGORY_COUNT = 10;

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

    #region Sub views

    /// <summary>
    /// Clickable view representing a single place card in the places grid,
    /// with interaction buttons (like, dislike, favorite, home, share) and a jump-in button.
    /// WARNING: do not click the card root to open the place detail — pointer-enter reveals
    /// a hover overlay whose Interactions row and JUMP IN button sit at the card's center,
    /// so a root click can favorite/teleport instead. Click <see cref="Thumbnail"/> (top of
    /// the card, never covered by the overlay) to open the detail popup.
    /// Also note the grid pools its 15 card instances: after an in-session refresh (search,
    /// category chip) the hierarchy index no longer matches the visual order, but element
    /// paths within one card always refer to that same card's content.
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
