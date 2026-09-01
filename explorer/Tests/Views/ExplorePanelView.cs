using ExplorerAutomation.Tests.Views.ExplorePanelSections;

namespace ExplorerAutomation.Tests.Views;

/// <summary>
/// View for the explore panel — the main tabbed content area of the Explorer.
/// Contains tab buttons to switch between sections and sub-views for each section
/// (events, places, communities, map, backpack, gallery, settings).
/// </summary>
public class ExplorePanelView() : BaseView(new(By.NAME, "ExplorePanelUI(Clone)"))
{
    protected override string ViewName => "ExplorePanelView";

    #region Elements

    public readonly Clickable CloseButton          = new(By.PATH, "//ExplorePanelUI(Clone)//CloseButton");
    // Tab buttons live inside //ExplorePanelUI(Clone)//TabSelector and are renamed by the
    // panel prefab to <Name>Tab via m_Name overrides on each prefab instance.
    public readonly Clickable EventsTabButton      = new(By.PATH, "//TabSelector/EventsTab");
    public readonly Clickable PlacesTabButton      = new(By.PATH, "//TabSelector/PlacesTab");
    public readonly Clickable CommunitiesTabButton = new(By.PATH, "//TabSelector/CommunitiesTab");
    public readonly Clickable MapTabButton         = new(By.PATH, "//TabSelector/MapTab");
    public readonly Clickable BackpackTabButton    = new(By.PATH, "//TabSelector/BackpackTab");
    public readonly Clickable GalleryTabButton     = new(By.PATH, "//TabSelector/GalleryTab");
    public readonly Clickable SettingsTabButton    = new(By.PATH, "//TabSelector/SettingsTab");

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

    #region Helper methods

    /// <summary>
    /// Closes the panel. Presses Escape first — it's the most reliable path because Unity's
    /// IClosable input handler catches it uniformly and every shortcut test (Map, Gallery,
    /// Backpack, …) proves Escape dismisses any section. The X button is only used as a
    /// fallback for the rare case where Escape is consumed elsewhere. Clicking the X first
    /// is unsafe for sections that absorb pointer input (Navmap drags, Gallery), which puts
    /// them in an interactive state that then swallows subsequent Escape.
    /// </summary>
    [AllureStep("Close the explore panel")]
    public void Close()
    {
        CommonStuff.AltDriver.PressKey(AltKeyCode.Escape);
        if (TryWaitForGone(5))
            return;

        Reporter.Log("Escape did not dismiss the panel — falling back to CloseButton click");
        CloseButton.Click(settleMs: 0);
        WaitForGone(15);
    }

    /// <summary>
    /// Best-effort WaitForGone. Returns true if the panel disappeared, false on timeout.
    /// We catch Exception (not just AssertionException) because AspectInjector / Allure
    /// wraps thrown exceptions in TargetInvocationException, which doesn't match a more
    /// specific catch.
    /// </summary>
    private bool TryWaitForGone(double timeoutSec)
    {
        // Shot-suppressed: this only answers "did Escape work?", and the panel closing is
        // every test's cleanup rather than anything one of them verifies.
        try { WaitForGone(timeoutSec, verificationShot: false); return true; }
        catch { return false; }
    }

    #endregion
}
