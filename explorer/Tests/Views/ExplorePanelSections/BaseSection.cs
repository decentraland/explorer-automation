namespace ExplorerAutomation.Tests.Views.ExplorePanelSections;

/// <summary>
/// Abstract base class for sections within a panel (e.g. explore panel tabs).
/// Wraps a section-level root locator so each section can independently check its own visibility.
/// </summary>
public abstract class BaseSection(Locatable sectionLocator) : BaseView(sectionLocator)
{
    // Sections are not MVC views; the panel that hosts them is. Its signal is what the
    // GraphicRaycaster guard approximated - the panel is interactable once it reports Shown.
    internal override AltObject WaitFor(double timeout, bool verificationShot)
    {
        ViewSignal.WaitForShown("ExplorePanelView", timeout);
        return base.WaitFor(timeout, verificationShot);
    }
}