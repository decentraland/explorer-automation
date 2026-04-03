namespace ExplorerAutomation.Tests.Views;

/// <summary>
/// View for the user passport popup that displays a player's profile,
/// with actions (add friend, chat, call) and tabs (overview, badges, photos).
/// </summary>
public class PassportView() : BaseView(new(By.NAME, "Passport(Clone)"))
{
    #region Elements

    public readonly Clickable AddFriendButton = new(By.NAME, "AddFriend");
    public readonly Clickable ChatButton      = new(By.NAME, "ChatButton");
    public readonly Clickable CallButton      = new(By.NAME, "VoiceChatButtonPassport");
    public readonly Clickable MenuButton      = new(By.NAME, "ContextMenuButton");
    public readonly Clickable CloseButton     = new(By.NAME, "Button_Close");

    public readonly Clickable OverviewTab = new(By.NAME, "OverviewSectionButton");
    public readonly Clickable BadgesTab   = new(By.NAME, "BadgesSectionButton");
    public readonly Clickable PhotosTab   = new(By.NAME, "PhotosSectionButton");

    #endregion
}
