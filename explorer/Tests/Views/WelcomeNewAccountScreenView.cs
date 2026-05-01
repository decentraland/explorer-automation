namespace ExplorerAutomation.Tests.Views;

/// <summary>
/// View for the new-user welcome screen that appears after a successful first
/// email+OTP login. Lets the user pick a username, accept Terms of Use,
/// optionally subscribe to the newsletter, randomize the avatar, and jump in.
/// </summary>
public class WelcomeNewAccountScreenView() :
    BaseView(new(By.NAME, "Lobby.NewAccount.Screen"))
{
    #region Elements

    public readonly Writable  UsernameInput            = new(By.PATH, "//Lobby.NewAccount.Screen//TextInput.Name/Input");
    public readonly Readable  UsernameCharacterCount   = new(By.PATH, "//Lobby.NewAccount.Screen//TextInput.Name/CharacterCount");
    public readonly Clickable SubscribeToggle          = new(By.NAME, "Subscribe.Toggle");
    public readonly Clickable TermsOfUseToggle         = new(By.NAME, "TermsOfUse.Toggle");
    public readonly Clickable JumpInButton             = new(By.NAME, "JumpIn.Button");
    public readonly Clickable RandomizeButton          = new(By.NAME, "NewRandomizeButton");
    public readonly Clickable BodyTypeDropdown         = new(By.NAME, "BodyTypeDropdown");
    public readonly Clickable BackButton               = new(By.NAME, "Back.Button Variant");
    public readonly Readable  CustomizeAvatarLaterText = new(By.PATH, "//Lobby.NewAccount.Screen//AvatarButtons.Container/Text (TMP)");

    #endregion
}
