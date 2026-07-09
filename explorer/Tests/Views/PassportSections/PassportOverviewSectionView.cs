namespace ExplorerAutomation.Tests.Views.PassportSections;

/// <summary>
/// Section view for the Overview tab within the passport panel,
/// containing badges, about me, and equipped wearables modules.
/// </summary>
public class PassportOverviewSectionView() : BaseView(new(By.NAME, "OverviewSectionPanel"))
{
    #region Elements

    public readonly Locatable MainScrollView = new(By.NAME, "Scroll View");

    #endregion

    #region Views

    public BadgesOverviewModule BadgesOverview { get; } = new();
    public AboutMeModule        AboutMe        { get; } = new();
    public EquippedItemsModule  EquippedItems  { get; } = new();

    #endregion

    #region Sub views

    /// <summary>
    /// Sub-view for the badges overview row within the overview section.
    /// </summary>
    public class BadgesOverviewModule() : BaseView(new(By.NAME, "BadgesOverview_PassportSubView"))
    {
        #region Elements

        public readonly Readable  BadgesTitle     = new(By.NAME, "BadgesTitle");
        public readonly Locatable BadgesContainer = new(By.NAME, "BadgesContainer");
        public readonly Locatable BadgeItem       = new(By.NAME, "BadgeOverviewItem_PassportField(Clone)");

        #endregion
    }

    /// <summary>
    /// Sub-view for the bio, additional info, and links module within the overview section.
    /// </summary>
    public class AboutMeModule() : BaseView(new(By.NAME, "UserDetailInfo_PassportSubView"))
    {
        #region Elements

        public readonly Readable  AboutMeTitle            = new(By.PATH, "//UserDetailInfo_PassportSubView/InfoTitle");
        public readonly Clickable EditAboutMeButton       = new(By.NAME, "Info_Button_Edit");
        public readonly Readable  BioText                 = new(By.NAME, "InfoField");
        public readonly Locatable AdditionalInfoContainer = new(By.PATH, "//UserDetailInfo_PassportSubView/AdditionalInfoContainer");
        public readonly Locatable AdditionalFieldItem     = new(By.NAME, "AdditionalField_PassportField(Clone)");
        public readonly Readable  LinksTitle              = new(By.NAME, "LinksTitle");
        public readonly Clickable EditLinksButton         = new(By.NAME, "Links_Button_Edit");
        public readonly Locatable LinksContainer          = new(By.NAME, "LinksContainer");
        public readonly Clickable LinkItem                = new(By.NAME, "Link_PassportField(Clone)");

        #endregion
    }

    /// <summary>
    /// Sub-view for the equipped wearables grid within the overview section.
    /// </summary>
    public class EquippedItemsModule() : BaseView(new(By.NAME, "EquippedItems_PassportSubView"))
    {
        #region Elements

        public readonly Readable  EquippedItemsTitle = new(By.PATH, "//EquippedItems_PassportSubView/InfoTitle");
        public readonly Locatable EquippedItemsGrid  = new(By.PATH, "//EquippedItems_PassportSubView/AdditionalInfoContainer");
        public readonly Locatable EquippedItemSlot   = new(By.NAME, "EquippedItem_PassportField(Clone)");
        public readonly Locatable EmptyEquippedSlot  = new(By.NAME, "EmptyItem");

        #endregion
    }

    #endregion
}
