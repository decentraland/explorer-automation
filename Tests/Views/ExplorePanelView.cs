using ExplorerAutomation.Tests.Views.ExplorePanelSections;

namespace ExplorerAutomation.Tests.Views;

public class ExplorePanelView() : BaseView(new(By.ID, "d5383a2a-d281-4fe8-b53b-fee873f32654"))
{
    public readonly Clickable CloseButton     = new(By.ID, "f507113e-bb78-4ddb-9d3e-4338e1f75dfe");

    public readonly Clickable EventsTab      = new(By.ID, "8b6ee3fb-097b-46b5-9d6a-e6ca21f737f0");
    public readonly Clickable PlacesTab      = new(By.ID, "261fa576-8df6-496e-82f0-dd11c2592086");
    public readonly Clickable CommunitiesTab = new(By.ID, "d696490d-ba13-4701-ad08-e617c2dbdd74");
    public readonly Clickable MapTab         = new(By.ID, "48d169c6-427d-4bb3-8bde-a2f06851b387");
    public readonly Clickable BackpackTab    = new(By.ID, "a5f6205e-84a2-4a68-9638-e1d27baf37e0");
    public readonly Clickable GalleryTab     = new(By.ID, "80fd3d49-bd26-4700-91ce-c50f97bce0b4");
    public readonly Clickable SettingsTab    = new(By.ID, "0107ddd9-a087-4fa5-885d-b47df8854ff9");

    public EventsSection      Events      { get; } = new();
    public PlacesSection      Places      { get; } = new();
    public CommunitiesSection Communities { get; } = new();
    public NavmapSection      Navmap      { get; } = new();
    public BackpackSection    Backpack    { get; } = new();
    public GallerySection     Gallery     { get; } = new();
    public SettingsSection    Settings    { get; } = new();
}
