namespace ExplorerAutomation.Tests.Views;

/// <summary>
/// View for the authentication main screen. Hosts both the cached-account state
/// (Jump Into World / Use a Different Account) and the logged-out
/// "Log in or Sign up" form (email + alternative providers).
/// </summary>
public class AuthenticationMainScreenView() :
    BaseView(new(By.NAME, "Authentication.MainScreen(Clone)"))
{
    #region Elements

    // Cached-account state (visible when an account is already saved locally)
    public readonly Clickable JumpIntoWorldButton        = new(By.NAME, "JumpIntoWorldButton");
    public readonly Clickable UseADifferentAccountButton = new(By.NAME, "UseAnotherAccountButton");

    // Logged-out state ("Log in or Sign up" form)
    public readonly Locatable LoginSelectionScreen = new(By.NAME, "LoginSelection.Screen");
    public readonly Writable  EmailInput           = new(By.PATH, "//EmailOTPDisalable.Container/EmailInputField/EmailInputField");
    public readonly Clickable NextButton           = new(By.NAME, "StartWithEmailButton");
    public readonly Clickable GoogleButton         = new(By.NAME, "Google.Button");
    public readonly Clickable MetamaskButton       = new(By.NAME, "Metamask.Button");
    public readonly Clickable MoreOptionsButton    = new(By.NAME, "MoreOptions.Button");

    #endregion
}
