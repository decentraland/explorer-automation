namespace ExplorerAutomation.Tests.Views;

/// <summary>
/// View for the sidebar nav rail; most icons open a submenu nested under their button.
/// </summary>
public class MainMenuView() : BaseView(new(By.NAME, "SidebarView"))
{
    #region Elements

    private const int RAIL_CLICK_ATTEMPTS = 2;
    private const double RAIL_CLICK_TIMEOUT = 4D;

    // Scoped by path where the name is generic (SettingsButton, Places, Gallery...).
    public readonly Clickable SettingsButton      = new(By.PATH, "//SidebarView/UpperLayout/SettingsButton");
    public readonly Clickable ShopButton          = new(By.PATH, "//SidebarView/UpperLayout/ShopButton");
    public readonly Clickable DiscoverButton      = new(By.PATH, "//SidebarView/UpperLayout/DiscoverButton");
    public readonly Clickable NotificationsButton = new(By.PATH, "//SidebarView/UpperLayout/NotificationsButton");
    public readonly Clickable MapButton           = new(By.PATH, "//SidebarView/UpperLayout/MapButton");
    public readonly Clickable SidebarSettingsButton = new(By.PATH, "//SidebarView/UpperLayout/SidebarConfigButton");

    // The press is wired to the OpenProfileButton child, not the widget root.
    public readonly Clickable ProfileButton = new(By.PATH, "//SidebarView/UpperLayout/SidebarProfileButton/OpenProfileButton");

    public readonly Clickable EmotesButton         = new(By.PATH, "//SidebarView/BottomLayout/EmotesButton");
    public readonly Clickable FriendsButton        = new(By.PATH, "//SidebarView/BottomLayout/FriendsButton");
    public readonly Clickable ChatButton           = new(By.NAME, "SidebarChatButton");
    public readonly Clickable GalleryButton        = new(By.PATH, "//SidebarView/BottomLayout/GalleryButton");
    public readonly Clickable AudioSettingsButton  = new(By.PATH, "//SidebarView/BottomLayout/AudioSettingsButton");
    public readonly Clickable SmartWearablesButton = new(By.NAME, "SidebarSmartWearablesButton");
    public readonly Clickable NearbyVoiceButton    = new(By.NAME, "NearbyVoice.Button");

    // Backpack, Events/Places/Communities, Marketplace(+Credits), Help, Camera and Skybox are no
    // longer rail buttons: see ProfileMenuView, the submenus below and MinimapView.Skybox.

    #endregion

    #region Views

    public NotificationsPanel Notifications { get; } = new();
    public DiscoverMenu Discover { get; } = new();
    public ShopMenu Shop { get; } = new();
    public SystemMenu System { get; } = new();
    public AudioMenu Audio { get; } = new();
    public GalleryMenu Gallery { get; } = new();

    #endregion

    #region Helper methods

    /// <summary>
    /// Opens a rail submenu; the icon toggles, so the submenu must start closed.
    /// </summary>
    [AllureStep("Open a sidebar submenu")]
    public void OpenSubMenu(Clickable railButton, BaseView subMenu)
    {
        ClickRailButton(railButton, () => subMenu.IsPresent(verificationShot: false));
        subMenu.WaitFor();
    }

    /// <summary>
    /// Clicks a rail button, clicking once more if nothing opened.
    /// </summary>
    /// <remarks>
    /// Opening the passport from the profile menu leaves that menu registered as open, and the
    /// next rail click only closes it.
    /// </remarks>
    [AllureStep("Click a sidebar rail button")]
    public void ClickRailButton(Clickable railButton, Func<bool> opened)
    {
        for (var attempt = 1; attempt <= RAIL_CLICK_ATTEMPTS; attempt++)
        {
            railButton.Click(settleMs: 0);

            var deadline = DateTime.UtcNow.AddSeconds(RAIL_CLICK_TIMEOUT);
            while (DateTime.UtcNow < deadline)
            {
                if (opened())
                    return;
                Thread.Sleep(250);
            }

            Reporter.Log($"Rail click {attempt} opened nothing — a stale submenu likely consumed it");
        }
    }

    /// <summary>Opens the Discover menu and picks one of its options.</summary>
    [AllureStep("Open a Discover menu option")]
    public void OpenFromDiscover(Clickable option)
    {
        OpenSubMenu(DiscoverButton, Discover);
        option.Click(settleMs: 0);
    }

    /// <summary>Opens the System menu and picks one of its options.</summary>
    [AllureStep("Open a System menu option")]
    public void OpenFromSystem(Clickable option)
    {
        OpenSubMenu(SettingsButton, System);
        option.Click(settleMs: 0);
    }

    /// <summary>Opens the Gallery menu and picks one of its options.</summary>
    [AllureStep("Open a Gallery menu option")]
    public void OpenFromGallery(Clickable option)
    {
        OpenSubMenu(GalleryButton, Gallery);
        option.Click(settleMs: 0);
    }

    #endregion

    #region Sub views

    /// <summary>
    /// Notifications dropdown opened by the bell. No ViewName: the voice widget shares its view type.
    /// </summary>
    public class NotificationsPanel() : BaseView(new(By.PATH, "//SidebarView//NotificationsButton/NotificationsMenu"))
    {
        #region Elements

        public readonly Readable TitleLabel = new(By.PATH, "//SidebarView//NotificationsButton/NotificationsMenu/Title");

        #endregion
    }

    /// <summary>
    /// Discover submenu: Places, Events and Communities.
    /// </summary>
    public class DiscoverMenu() : BaseView(new(By.PATH, "//SidebarView//DiscoverButton/DiscoverMenu"))
    {
        protected override string ViewName => "DiscoverMenuView";

        #region Elements

        public readonly Readable  TitleLabel        = new(By.PATH, "//DiscoverMenu/Title");
        public readonly Clickable PlacesOption      = new(By.PATH, "//DiscoverMenu/MenuLayout/Places");
        public readonly Clickable EventsOption      = new(By.PATH, "//DiscoverMenu/MenuLayout/Events");
        public readonly Clickable CommunitiesOption = new(By.PATH, "//DiscoverMenu/MenuLayout/Communities");

        #endregion
    }

    /// <summary>
    /// Shop submenu: credits and marketplace banners. Every entry opens the browser, so presence only.
    /// </summary>
    public class ShopMenu() : BaseView(new(By.PATH, "//SidebarView//ShopButton/ShopMenu"))
    {
        protected override string ViewName => "ShopMenuView";

        #region Elements

        public readonly Readable  TitleLabel             = new(By.PATH, "//ShopMenu/Title");
        public readonly Locatable CreditsPanel           = new(By.PATH, "//ShopMenu/CreditsContainer");
        public readonly Locatable BuyCreditsButton       = new(By.PATH, "//ShopMenu/CreditsContainer/BuyCreditsButton");
        public readonly Locatable ExploreWearablesBanner = new(By.PATH, "//ShopMenu/ExploreWearablesBanner");
        public readonly Locatable ExploreEmotesBanner    = new(By.PATH, "//ShopMenu/ExploreEmotesBanner");

        #endregion
    }

    /// <summary>
    /// System submenu (gear icon). No ViewName: an unrelated client view shares the SystemMenuView name.
    /// </summary>
    public class SystemMenu() : BaseView(new(By.PATH, "//SidebarView//SettingsButton/SystemMenu"))
    {
        #region Elements

        public readonly Readable  TitleLabel           = new(By.PATH, "//SystemMenu/Title");
        public readonly Clickable SettingsOption       = new(By.PATH, "//SystemMenu/TopMenuLayout/Settings");
        public readonly Clickable ControlsOption       = new(By.PATH, "//SystemMenu/TopMenuLayout/Controls");
        public readonly Clickable ContactSupportOption = new(By.PATH, "//SystemMenu/BottomMenuLayout/ContactSupport");
        public readonly Clickable ReportBugOption      = new(By.PATH, "//SystemMenu/BottomMenuLayout/ReportBug");
        public readonly Clickable DiscordOption        = new(By.PATH, "//SystemMenu/BottomMenuLayout/Discord");
        public readonly Clickable FaqOption            = new(By.PATH, "//SystemMenu/BottomMenuLayout/FAQ");

        #endregion
    }

    /// <summary>
    /// Audio submenu with the volume sliders.
    /// </summary>
    public class AudioMenu() : BaseView(new(By.PATH, "//SidebarView//AudioSettingsButton/AudioMenu"))
    {
        protected override string ViewName => "AudioMenuView";

        #region Elements

        public readonly Readable  TitleLabel    = new(By.PATH, "//AudioMenu/Title");
        public readonly Locatable MasterSlider  = new(By.PATH, "//AudioMenu/ConfigsLayout/Master");
        public readonly Locatable UiSlider      = new(By.PATH, "//AudioMenu/ConfigsLayout/UI");
        public readonly Locatable AvatarSlider  = new(By.PATH, "//AudioMenu/ConfigsLayout/AvatarEmotesSFX");
        public readonly Locatable InWorldSlider = new(By.PATH, "//AudioMenu/ConfigsLayout/InWorld");

        #endregion
    }

    /// <summary>
    /// Gallery submenu (camera icon): camera reel and photo mode.
    /// </summary>
    public class GalleryMenu() : BaseView(new(By.PATH, "//SidebarView//GalleryButton/GalleryMenu"))
    {
        protected override string ViewName => "GalleryMenuView";

        #region Elements

        public readonly Readable  TitleLabel     = new(By.PATH, "//GalleryMenu/Title");
        public readonly Clickable GalleryOption  = new(By.PATH, "//GalleryMenu/MenuLayout/Gallery");
        public readonly Clickable TakeShotOption = new(By.PATH, "//GalleryMenu/MenuLayout/TakeShot");

        #endregion
    }

    #endregion
}
