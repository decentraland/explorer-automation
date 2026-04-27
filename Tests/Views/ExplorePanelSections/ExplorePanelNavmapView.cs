namespace ExplorerAutomation.Tests.Views.ExplorePanelSections;

/// <summary>
/// Section view for the Map tab within the explore panel, displaying the Decentraland world navigation map.
/// Contains category filters, navigation controls, search functionality, and a place info panel.
/// </summary>
public class ExplorePanelNavmapView() : BaseSection(new(By.ID, "106af455-ca73-4241-9474-b82d160d816e"))
{
    #region Elements

    // Category filter toggles
    public readonly Clickable AllCategoryButton       = new(By.PATH, "//NavmapSection/Navmap/CategoriesFilter/All");
    public readonly Clickable FavoritesCategoryButton = new(By.PATH, "//NavmapSection/Navmap/CategoriesFilter/Favorites");
    public readonly Clickable SocialCategoryButton    = new(By.PATH, "//NavmapSection/Navmap/CategoriesFilter/Social");
    public readonly Clickable MusicCategoryButton     = new(By.PATH, "//NavmapSection/Navmap/CategoriesFilter/Music");
    public readonly Clickable ArtCategoryButton       = new(By.PATH, "//NavmapSection/Navmap/CategoriesFilter/Art");
    public readonly Clickable GameCategoryButton      = new(By.PATH, "//NavmapSection/Navmap/CategoriesFilter/Game");
    public readonly Clickable FashionCategoryButton   = new(By.PATH, "//NavmapSection/Navmap/CategoriesFilter/Fashion");
    public readonly Clickable EducationCategoryButton = new(By.PATH, "//NavmapSection/Navmap/CategoriesFilter/Education");
    public readonly Clickable ShopCategoryButton      = new(By.PATH, "//NavmapSection/Navmap/CategoriesFilter/Shop");
    public readonly Clickable SportCategoryButton     = new(By.PATH, "//NavmapSection/Navmap/CategoriesFilter/Sport");
    public readonly Clickable BusinessCategoryButton  = new(By.PATH, "//NavmapSection/Navmap/CategoriesFilter/Business");

    // Navigation controls
    public readonly Clickable ZoomInButton          = new(By.PATH, "//NavigationPanel//Zoom/Plus");
    public readonly Clickable ZoomOutButton         = new(By.PATH, "//NavigationPanel//Zoom/Minus");
    public readonly Clickable CenterToHomeButton    = new(By.PATH, "//NavigationPanel//CenterToHome");
    public readonly Clickable CenterToPlayerButton  = new(By.PATH, "//NavigationPanel//CenterToPlayer");
    public readonly Clickable OpenLayersPanelButton = new(By.PATH, "//NavigationPanel//OpenLayersPanel");

    // Search bar
    public readonly Writable SearchBar = new(By.PATH, "//PlacesAndEventsPanel//SearchBarContainer/SearchBar");

    // Search results sort buttons
    public readonly Clickable SortByMostActiveButton = new(By.PATH, "//SearchPlaces//Sorts/MostActive");
    public readonly Clickable SortByBestRatedButton  = new(By.PATH, "//SearchPlaces//Sorts/BestRated");
    public readonly Clickable SortByNewestButton     = new(By.PATH, "//SearchPlaces//Sorts/Newest");

    // Search results pagination
    public readonly Locatable SearchResultsScrollView = new(By.PATH, "//SearchPlaces//Results/Scroll View");
    public readonly Clickable NextPageButton          = new(By.PATH, "//SearchPlaces//Pagination/NextPageButton");
    public readonly Clickable PrevPageButton          = new(By.PATH, "//SearchPlaces//Pagination/PrevPageButton");

    // Worlds warning notification
    public readonly Locatable WorldsWarningNotification = new(By.NAME, "WorldsWarningNotification");
    public readonly Readable  WorldsWarningText         = new(By.PATH, "//WorldsWarningNotification/Text (TMP)");
    public readonly Clickable WorldsWarningCloseButton  = new(By.PATH, "//WorldsWarningNotification/CloseButton");

    // Satellite view credits
    public readonly Clickable SatelliteCreditsLink = new(By.PATH, "//SatelliteCreditsPanel/HyperLinkTMPro");

    #endregion

    #region Views

    public FiltersPanel  Filters   { get; } = new();
    public PlaceInfoCard PlaceInfo { get; } = new();

    #endregion

    #region Sub views

    /// <summary>
    /// The layers/filters panel revealed when the OpenLayersPanel button is clicked.
    /// Contains toggles for live events, POIs, mini-games, layer visibility, and map view type.
    /// </summary>
    public class FiltersPanel() : BaseView(new(By.NAME, "FiltersContainer"))
    {
        #region Elements

        public readonly Readable  Title               = new(By.PATH, "//FiltersContainer/Title");
        public readonly Clickable LiveEventsToggle    = new(By.PATH, "//FiltersContainer/FilterSection/LiveEvents/Toggle");
        public readonly Clickable POIsToggle          = new(By.PATH, "//FiltersContainer/FilterSection/POIs/Toggle");
        public readonly Clickable MiniGamesToggle     = new(By.PATH, "//FiltersContainer/FilterSection/MiniGames/Toggle");
        public readonly Clickable LayerToggle         = new(By.PATH, "//FiltersContainer/FilterSection (1)/LayerToggle/Toggle");
        public readonly Clickable SatelliteViewButton = new(By.PATH, "//FiltersContainer/FilterSection (4)/GameObject/Satellite");
        public readonly Clickable ParcelViewButton    = new(By.PATH, "//FiltersContainer/FilterSection (4)/GameObject/Parcel");

        #endregion
    }

    /// <summary>
    /// The place info card shown when a place is selected on the map.
    /// Displays thumbnail, live event badge, stats, interaction buttons, and content tabs.
    /// </summary>
    public class PlaceInfoCard() : BaseView(new(By.NAME, "PlaceSectionLoaded"))
    {
        #region Elements

        public readonly Locatable PlaceImage            = new(By.PATH, "//PlaceSectionLoaded//Thumbnail/PlaceImage");
        public readonly Readable  LiveEventBadgeText    = new(By.PATH, "//PlaceSectionLoaded//Live/Badge/Text (TMP)");
        public readonly Readable  LiveEventNameLabel    = new(By.PATH, "//PlaceSectionLoaded//Live/EventNameLabel");
        public readonly Readable  PlaceName             = new(By.PATH, "//PlaceSectionLoaded//InfoAndActions/PlaceName");
        public readonly Readable  CreatorName           = new(By.PATH, "//PlaceSectionLoaded//InfoAndActions/CreatorName");
        public readonly Readable  PlayersCountText      = new(By.PATH, "//PlaceSectionLoaded//StatsSection/PlayersCountText");
        public readonly Readable  LikesText             = new(By.PATH, "//PlaceSectionLoaded//StatsSection/LikesText");
        public readonly Clickable StartNavigationButton = new(By.PATH, "//PlaceSectionLoaded//InfoAndActions/StartNavigation");
        public readonly Clickable JumpInButton          = new(By.PATH, "//PlaceSectionLoaded//InfoAndActions/JumpIn");
        public readonly Clickable LikeButton            = new(By.PATH, "//PlaceSectionLoaded//Interactions/LikeButton");
        public readonly Clickable DislikeButton         = new(By.PATH, "//PlaceSectionLoaded//Interactions/DislikeButton");
        public readonly Clickable FavoriteButton        = new(By.PATH, "//PlaceSectionLoaded//Interactions/FavoriteButton");
        public readonly Clickable SetHomeButton         = new(By.PATH, "//PlaceSectionLoaded//Interactions/HomeButton");
        public readonly Clickable DonationsButton       = new(By.PATH, "//PlaceSectionLoaded//Interactions/DonationsButton");
        public readonly Clickable ShareButton           = new(By.PATH, "//PlaceSectionLoaded//Interactions/ShareButton");
        public readonly Clickable OverviewTabButton     = new(By.PATH, "//PlaceSectionLoaded//Tabs/Overview");
        public readonly Clickable PhotosTabButton       = new(By.PATH, "//PlaceSectionLoaded//Tabs/Photos");
        public readonly Clickable EventsTabButton       = new(By.PATH, "//PlaceSectionLoaded//Tabs/Events");

        #endregion
    }

    #endregion
}
