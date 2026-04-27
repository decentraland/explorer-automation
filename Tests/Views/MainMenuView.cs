namespace ExplorerAutomation.Tests.Views;

/// <summary>
/// View for the main menu sidebar that provides navigation buttons for all major sections
/// of the Explorer (events, places, communities, map, backpack, gallery, settings, etc.).
/// </summary>
public class MainMenuView() : BaseView(new(By.ID, "bab6108c-7cce-45a1-9bcd-40412c1f435e"))
{
    #region Elements

    public readonly Clickable ProfileButton = new(By.NAME, "SidebarProfileButton");
    public readonly Clickable NotificationsButton = new(By.NAME, "SidebarNotificationsButton");
    public readonly Clickable EventsButton = new(By.NAME, "EventsButton");
    public readonly Clickable PlacesButton = new(By.NAME, "PlacesButton");
    public readonly Clickable CommunitiesButton = new(By.NAME, "SidebarCommunitiesButton");
    public readonly Clickable MapButton = new(By.NAME, "SidebarMapButton");
    public readonly Clickable BackpackButton = new(By.NAME, "SidebarBackpackButton");
    public readonly Clickable MarketplaceButton = new(By.NAME, "SidebarMarketplaceButton");
    public readonly Clickable GalleryButton = new(By.NAME, "SidebarGalleryButton");
    public readonly Clickable SettingsButton = new(By.NAME, "SidebarSettingsButton");
    public readonly Clickable ControlsButton = new(By.NAME, "SidebarControlsScreenButton");
    public readonly Clickable HelpButton = new(By.NAME, "SidebarHelpButton");
    public readonly Clickable SidebarSettingsButton = new(By.NAME, "SidebarConfigButton");

    #endregion
}
