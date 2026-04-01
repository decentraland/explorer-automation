namespace ExplorerAutomation.Tests.Views;

public class PassportView() : BaseView(new(By.NAME, "Passport(Clone)"))
{
    public readonly Clickable AddFriendButton = new(By.NAME, "AddFriend");
    public readonly Clickable ChatButton      = new(By.NAME, "ChatButton");
    public readonly Clickable CallButton      = new(By.NAME, "VoiceChatButtonPassport");
    public readonly Clickable MenuButton      = new(By.NAME, "ContextMenuButton");
    public readonly Clickable CloseButton     = new(By.NAME, "Button_Close");

    public readonly Clickable OverviewTab = new(By.NAME, "OverviewSectionButton");
    public readonly Clickable BadgesTab   = new(By.NAME, "BadgesSectionButton");
    public readonly Clickable PhotosTab   = new(By.NAME, "PhotosSectionButton");
}
