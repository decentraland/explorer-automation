namespace ExplorerAutomation.Tests.Views;

/// <summary>
/// View for the main menu sidebar that provides navigation buttons for all major sections
/// of the Explorer (events, places, communities, backpack, gallery, settings, friends, chat, etc.).
/// </summary>
public class MainMenuView() : BaseView(new(By.NAME, "SidebarView"))
{
    #region Elements

    // Upper layout — buttons that ship as their own prefab (root GameObject keeps the prefab name).
    public readonly Clickable ProfileButton            = new(By.NAME, "SidebarProfileButton");
    public readonly Clickable NotificationsButton      = new(By.NAME, "SidebarNotificationsButton");
    public readonly Clickable MarketplaceCreditsButton = new(By.NAME, "SidebarMarketplaceCreditsButton");
    public readonly Clickable CommunitiesButton        = new(By.NAME, "SidebarCommunitiesButton");
    public readonly Clickable BackpackButton           = new(By.NAME, "SidebarBackpackButton");
    public readonly Clickable MarketplaceButton        = new(By.NAME, "SidebarMarketplaceButton");
    public readonly Clickable GalleryButton            = new(By.NAME, "SidebarGalleryButton");
    public readonly Clickable SettingsButton           = new(By.NAME, "SidebarSettingsButton");
    public readonly Clickable HelpButton               = new(By.NAME, "SidebarHelpButton");
    public readonly Clickable SidebarSettingsButton    = new(By.NAME, "SidebarConfigButton");

    // Buttons whose GameObject names come from m_Name overrides in SidebarUI_UpperLayout.prefab.
    public readonly Clickable EventsButton = new(By.NAME, "EventsButton");
    public readonly Clickable PlacesButton = new(By.NAME, "PlacesButton");

    // Bottom layout — voice chat, portable experiences, skybox, camera, emotes, social.
    public readonly Clickable NearbyVoiceButton    = new(By.NAME, "NearbyVoice.Button");
    public readonly Clickable SmartWearablesButton = new(By.NAME, "SidebarSmartWearablesButton");
    public readonly Clickable SkyboxButton         = new(By.NAME, "SidebarSkyboxButton");
    public readonly Clickable InWorldCameraButton  = new(By.NAME, "SidebarInWorldCameraButton");
    public readonly Clickable EmoteWheelButton     = new(By.NAME, "SidebarEmoteWheelButton");
    public readonly Clickable FriendsButton        = new(By.NAME, "SidebarFriendsButton");
    public readonly Clickable ChatButton           = new(By.NAME, "SidebarChatButton");

    // NOTE: this build's sidebar has no Map button (SidebarMapButton) and no keyboard-shortcuts
    // button (SidebarControlsScreenButton) — verified via UiDump `sub //SidebarView/UpperLayout/*
    // --all` + `//SidebarView/BottomLayout/* --all` on build dev_b97439fc. The map is reachable
    // via the M shortcut / explore-panel Map tab, and the controls panel via the help menu ([H]).

    #endregion

    #region Views

    public NotificationsPanel Notifications { get; } = new();
    public HelpMenu Help { get; } = new();

    #endregion

    #region Sub views

    /// <summary>
    /// Dropdown panel listing the account's notifications. Opened by clicking the sidebar
    /// notifications bell; a second click on the bell (or Escape) closes it again.
    /// The GameObject lives under the bell button and is disabled while closed.
    /// </summary>
    public class NotificationsPanel() : BaseView(new(By.PATH, "//SidebarNotificationsButton/NotificationsMenu"))
    {
        #region Elements

        public readonly Readable TitleLabel = new(By.PATH, "//SidebarNotificationsButton/NotificationsMenu/Title");

        #endregion
    }

    /// <summary>
    /// Context menu opened by the sidebar help button. Holds the "Mouse and Key Controls"
    /// entry (opens the controls panel) plus external support links (FAQ, Contact Support,
    /// Discord) that open the system browser. Closes on Escape or outside click.
    /// </summary>
    public class HelpMenu() : BaseView(new(By.NAME, "SidebarHelpMenuView"))
    {
        #region Elements

        public readonly Clickable MouseAndKeyControlsButton = new(By.NAME, "MouseAndKeyControlsButton");
        public readonly Clickable FaqButton                 = new(By.NAME, "FAQButton");
        public readonly Clickable ContactSupportButton      = new(By.NAME, "ContactSupportButton");
        public readonly Clickable DiscordButton             = new(By.NAME, "DiscordButton");

        #endregion
    }

    #endregion
}
