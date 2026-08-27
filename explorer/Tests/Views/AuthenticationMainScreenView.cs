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

    // The "Welcome back <username>" greeting label rendered on the
    // cached-account sub-screen (`Lobby.ExistingAccount.Screen`). The
    // Explorer's TokenFileAuthenticator lands a freshly-authenticated user
    // here after consuming `auth-token-bridge.txt` — making this the
    // identity surface for Flow 2's `WebFirstLoginUsernameAssert`. Same
    // surface for recurrent-OTP login.
    //
    // Renders the FULL greeting (e.g. "Welcome back Gab"), so the C#
    // assertion either string-matches "Welcome back <expected>" or strips
    // the prefix before comparing.
    //
    // Verified via DiagnoseAuthScreenUsernameLabel against build dev_bac52c80
    // (2026-05-22). The disambiguating path prefix is required because
    // multiple elements named "Title" exist scene-wide.
    public readonly Readable UsernameLabel =
        new(By.PATH, "//Lobby.ExistingAccount.Screen//Title");

    // Logged-out state ("Log in or Sign up" form)
    public readonly Locatable LoginSelectionScreen = new(By.NAME, "LoginSelection.Screen");
    public readonly Writable  EmailInput           = new(By.PATH, "//EmailOTPDisalable.Container/EmailInputField/EmailInputField");
    public readonly Clickable NextButton           = new(By.NAME, "StartWithEmailButton");
    public readonly Clickable GoogleButton         = new(By.NAME, "Google.Button");
    public readonly Clickable MetamaskButton       = new(By.NAME, "Metamask.Button");
    public readonly Clickable MoreOptionsButton    = new(By.NAME, "MoreOptions.Button");

    #endregion
}
