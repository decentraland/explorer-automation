namespace ExplorerAutomation.Tests.Views;

/// <summary>
/// View for the profile submenu opened from the sidebar profile picture.
/// </summary>
public class ProfileMenuView() : BaseView(new(By.PATH, "//SidebarProfileButton/ProfileMenu"))
{
    protected override string ViewName => "SidebarProfileMenuView";

    #region Elements

    public readonly Clickable BadgesButton    = new(By.PATH, "//SidebarProfileButton/ProfileMenu/MidMenuLayout/Badges");
    // Opens the own passport.
    public readonly Clickable ViewProfileButton = new(By.PATH, "//SidebarProfileButton/ProfileMenu/MidMenuLayout/Profile");
    public readonly Clickable BackpackButton  = new(By.PATH, "//SidebarProfileButton/ProfileMenu/MidMenuLayout/Backpack");
    public readonly Clickable SignOutButton   = new(By.PATH, "//SidebarProfileButton/ProfileMenu/BottomMenuLayout/SignOut");
    public readonly Clickable ExitButton      = new(By.PATH, "//SidebarProfileButton/ProfileMenu/BottomMenuLayout/Exit");

    #endregion
}
