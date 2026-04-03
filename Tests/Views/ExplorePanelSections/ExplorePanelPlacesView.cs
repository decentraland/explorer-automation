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

    public Clickable[] CategoryButtons { get; }
    public PlaceCard[] Cards { get; }

    #endregion

    #region Setup

    public ExplorePanelPlacesView() : base(new(By.ID, "c8ab66c3-bc0e-4b95-bca8-63984be025a5"))
    {
        CategoryButtons = new Clickable[CATEGORY_COUNT];
        for (var i = 0; i < CATEGORY_COUNT; i++)
            CategoryButtons[i] = new(By.PATH, $"//Places/Content/CategoriesContainer/CategoryButton(Clone)[{i}]");

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
    /// </summary>
    public class PlaceCard : BaseClickableView
    {
        #region Elements

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
