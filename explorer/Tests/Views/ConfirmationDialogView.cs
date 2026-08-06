namespace ExplorerAutomation.Tests.Views;

/// <summary>
/// View for the generic yes/cancel confirmation dialog spawned at the scene root
/// (e.g. "Are you sure you want to delete this Outfit?").
/// </summary>
public class ConfirmationDialogView() : BaseView(new(By.NAME, "ConfirmationDialog(Clone)"))
{
    #region Elements

    public readonly Clickable YesButton    = new(By.PATH, "//ConfirmationDialog(Clone)//YesButton");
    public readonly Clickable CancelButton = new(By.PATH, "//ConfirmationDialog(Clone)//CancelButton");

    #endregion
}
