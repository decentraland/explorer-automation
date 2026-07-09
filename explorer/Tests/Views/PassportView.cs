using ExplorerAutomation.Tests.Views.PassportSections;

namespace ExplorerAutomation.Tests.Views;

/// <summary>
/// View for the user passport popup that displays a player's profile,
/// with actions (add friend, chat, call) and tabs (overview, badges, photos, creations).
/// </summary>
public class PassportView() : BaseView(new(By.NAME, "Passport(Clone)"))
{
    #region Elements

    public readonly Clickable BackgroundCloseButton = new(By.NAME, "Background_Close");

    // Action bar
    public readonly Clickable AddFriendButton = new(By.NAME, "AddFriend");
    public readonly Clickable ChatButton      = new(By.NAME, "ChatButton");
    public readonly Clickable CallButton      = new(By.NAME, "VoiceChatButtonPassport");
    public readonly Clickable MenuButton      = new(By.NAME, "ContextMenuButton");
    public readonly Clickable CloseButton     = new(By.NAME, "Button_Close");

    // Header
    public readonly Readable  UserNameText          = new(By.NAME, "UserName");
    public readonly Readable  UserIDText            = new(By.NAME, "UserID");
    public readonly Clickable CopyNameButton        = new(By.PATH, "//UserNameContainer/CopyButton");
    public readonly Clickable CopyIDButton          = new(By.PATH, "//UserIDContainer/CopyButton");
    public readonly Clickable EditNameButton        = new(By.NAME, "EditNameButton");
    public readonly Clickable ChangeNameColorButton = new(By.NAME, "ChangeColorButton");
    public readonly Locatable OfficialMarkImage     = new(By.NAME, "OfficialMark");
    public readonly Locatable VerifiedMarkImage     = new(By.NAME, "VerifiedMark");
    public readonly Locatable AvatarPreviewImage    = new(By.NAME, "PreviewRawImage");

    // Tabs
    public readonly Clickable OverviewTab  = new(By.NAME, "OverviewSectionButton");
    public readonly Clickable BadgesTab    = new(By.NAME, "BadgesSectionButton");
    public readonly Clickable PhotosTab    = new(By.NAME, "PhotosSectionButton");
    public readonly Clickable CreationsTab = new(By.NAME, "CreationsSectionButton");

    #endregion

    #region Views

    public PassportOverviewSectionView Overview { get; } = new();

    #endregion
}
