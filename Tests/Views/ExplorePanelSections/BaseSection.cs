namespace ExplorerAutomation.Tests.Views.ExplorePanelSections;

/// <summary>
/// Abstract base class for sections within a panel (e.g. explore panel tabs).
/// Wraps a section-level root locator so each section can independently check its own visibility.
/// </summary>
public abstract class BaseSection(Locatable sectionLocator) : BaseView(sectionLocator)
{
    private static readonly Locatable PanelRoot = new(By.NAME, "ExplorePanelUI(Clone)");

    /// <summary>
    /// Waits for the section's root, plus the parent ExplorePanel's GraphicRaycaster to be
    /// re-enabled. The MVC ViewBase disables the raycaster while the show animation plays;
    /// without this guard, clicks on inner controls (CloseButton, tab buttons) right after
    /// the section becomes findable get eaten because the raycaster ignores them.
    /// </summary>
    public override AltObject WaitFor(double timeout = 20D)
    {
        var altObj = base.WaitFor(timeout);
        try
        {
            var panel = PanelRoot.WaitFor(5);
            panel.WaitForComponentProperty(
                "UnityEngine.UI.GraphicRaycaster",
                "enabled",
                true,
                "UnityEngine.UI",
                timeout: 20);
            // Some sections (Map, Gallery) load remote content after the section is shown,
            // and the raycaster can flicker off again during that load. Brief settle pause
            // lets the panel finalize its interactable state before tests touch it.
            Thread.Sleep(750);
        }
        catch (AssertionException) { /* panel root may not be present in some sub-section flows */ }
        return altObj;
    }
}