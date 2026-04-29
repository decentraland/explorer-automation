namespace ExplorerAutomation.Tests.Views;

/// <summary>
/// View for the scene loading screen with the 0→100% progress bar that shows after
/// JumpIntoWorld and stays up until the realm + scene streaming finishes. When this
/// disappears, the world is fully loaded and HUD inputs are wired up.
/// </summary>
public class LoadingScreenView() : BaseView(new(By.NAME, "SceneLoadingScreen"))
{
}
