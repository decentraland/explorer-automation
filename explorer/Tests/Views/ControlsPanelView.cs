namespace ExplorerAutomation.Tests.Views;

/// <summary>
/// View for the full-screen "Mouse and Key Controls" cheat-sheet panel, opened from the
/// sidebar help menu ("Mouse and Key Controls") or with the H shortcut. The panel prefab is
/// instantiated once and toggled by enabling/disabling, so the root is only findable while open.
/// </summary>
public class ControlsPanelView() : BaseView(new(By.NAME, "ControlsPanel(Clone)"))
{
    #region Elements

    public readonly Clickable ExitButton  = new(By.PATH, "//ControlsPanel(Clone)//ExitButton");
    public readonly Readable  HeaderLabel = new(By.PATH, "//ControlsPanel(Clone)//Header");

    #endregion
}
