namespace ExplorerAutomation.Tests.Views.ExplorePanelSections;

/// <summary>
/// Abstract base class for sections within a panel (e.g. explore panel tabs).
/// Wraps a section-level root locator so each section can independently check its own visibility.
/// </summary>
public abstract class BaseSection(Locatable sectionLocator) : BaseView(sectionLocator)
{
}