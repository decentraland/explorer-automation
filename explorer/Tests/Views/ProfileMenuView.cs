namespace ExplorerAutomation.Tests.Views;

/// <summary>
/// View for the profile menu popup that opens when the sidebar profile button is clicked.
/// Hosts account-level actions including Sign Out, Exit App, and links to Privacy Policy
/// and Terms of Service.
/// </summary>
public class ProfileMenuView() : BaseView(new(By.NAME, "ProfileMenuView"))
{
    #region Elements

    public readonly Clickable SignOutButton         = new(By.NAME, "SignOutButton");
    public readonly Clickable ExitButton            = new(By.NAME, "ExitButton");
    public readonly Clickable PrivacyPolicyButton   = new(By.NAME, "PrivacyPolicyButton");
    public readonly Clickable TermsOfServiceButton  = new(By.NAME, "TermsOfServiceButton");

    #endregion
}
