namespace ExplorerAutomation.Tests.Views;

/// <summary>
/// View for the community details popup ("community card") that appears when clicking a
/// community in the explore panel's Communities section. The panel lives at the scene root
/// (not inside ExplorePanelUI) and is disabled — not destroyed — when closed.
/// For private communities the Contents area is replaced by an access-restriction notice.
/// </summary>
public class CommunityDetailView() : BaseView(new(By.NAME, "CommunityCard(Clone)"))
{
    #region Elements

    public readonly Clickable CloseButton   = new(By.PATH, "//CommunityCard(Clone)//Button_Close");
    // GameObject names in the header contain spaces ("Community name").
    public readonly Readable  CommunityName = new(By.PATH, "//CommunityCard(Clone)//Header//Community name");
    public readonly Readable  Description   = new(By.PATH, "//CommunityCard(Clone)//Header//Community description");

    // Header membership CTA — a swap pair, only one active at a time (same pattern as the
    // Minimap's Collapse/Expand buttons).
    public readonly Clickable JoinButton   = new(By.PATH, "//CommunityCard(Clone)//Header//Join");
    public readonly Clickable JoinedButton = new(By.PATH, "//CommunityCard(Clone)//Header//Joined");

    // Content section tabs under the header.
    public readonly Clickable AnnouncementsSectionButton = new(By.PATH, "//CommunityCard(Clone)//Sections/AnnouncementsSectionButton");
    public readonly Clickable MembersSectionButton       = new(By.PATH, "//CommunityCard(Clone)//Sections/MembersSectionButton");
    public readonly Clickable PlacesSectionButton        = new(By.PATH, "//CommunityCard(Clone)//Sections/PlacesSectionButton");
    public readonly Clickable PhotosSectionButton        = new(By.PATH, "//CommunityCard(Clone)//Sections/PhotosSectionButton");

    // Content containers toggled by the section buttons (Announcements is the default).
    public readonly Locatable AnnouncementsContent = new(By.PATH, "//CommunityCard(Clone)//Contents/Content/Announcements");
    public readonly Locatable MembersContent       = new(By.PATH, "//CommunityCard(Clone)//Contents/Content/Members");
    public readonly Locatable PlacesContent        = new(By.PATH, "//CommunityCard(Clone)//Contents/Content/Places");
    public readonly Locatable PhotosContent        = new(By.PATH, "//CommunityCard(Clone)//Contents/Content/Gallery");

    // Shown instead of Contents when the community is private and the user is not a member.
    public readonly Locatable PrivateAccessRestriction = new(By.PATH, "//CommunityCard(Clone)//PrivateAccessRestriction_Contents");

    #endregion
}
