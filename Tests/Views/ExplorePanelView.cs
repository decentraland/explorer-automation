using ExplorerAutomation.Tests.Views.ExplorePanelSections;

namespace ExplorerAutomation.Tests.Views;

/// <summary>
/// View for the explore panel — the main tabbed content area of the Explorer.
/// Contains tab buttons to switch between sections and sub-views for each section
/// (events, places, communities, map, backpack, gallery, settings).
/// </summary>
public class ExplorePanelView() : BaseView(new(By.ID, "d5383a2a-d281-4fe8-b53b-fee873f32654"))
{
    #region Elements

    public readonly Clickable CloseButton          = new(By.ID, "f507113e-bb78-4ddb-9d3e-4338e1f75dfe");
    public readonly Clickable EventsTabButton      = new(By.ID, "8b6ee3fb-097b-46b5-9d6a-e6ca21f737f0");
    public readonly Clickable PlacesTabButton      = new(By.ID, "261fa576-8df6-496e-82f0-dd11c2592086");
    public readonly Clickable CommunitiesTabButton = new(By.ID, "d696490d-ba13-4701-ad08-e617c2dbdd74");
    public readonly Clickable MapTabButton         = new(By.ID, "48d169c6-427d-4bb3-8bde-a2f06851b387");
    public readonly Clickable BackpackTabButton    = new(By.ID, "a5f6205e-84a2-4a68-9638-e1d27baf37e0");
    public readonly Clickable GalleryTabButton     = new(By.ID, "80fd3d49-bd26-4700-91ce-c50f97bce0b4");
    public readonly Clickable SettingsTabButton    = new(By.ID, "0107ddd9-a087-4fa5-885d-b47df8854ff9");

    #endregion

    #region Views

    public ExplorePanelEventsView      Events      { get; } = new();
    public ExplorePanelPlacesView      Places      { get; } = new();
    public ExplorePanelCommunitiesView Communities { get; } = new();
    public ExplorePanelNavmapView      Navmap      { get; } = new();
    public ExplorePanelBackpackView    Backpack    { get; } = new();
    public ExplorePanelGalleryView     Gallery     { get; } = new();
    public ExplorePanelSettingsView    Settings    { get; } = new();

    #endregion
}
