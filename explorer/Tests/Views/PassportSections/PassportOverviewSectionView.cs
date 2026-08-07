namespace ExplorerAutomation.Tests.Views.PassportSections;

/// <summary>
/// Section view for the Overview tab within the passport panel,
/// containing badges, about me, and equipped wearables modules.
/// </summary>
public class PassportOverviewSectionView() : BaseView(new(By.NAME, "OverviewSectionPanel"))
{
    #region Elements

    // "Scroll View" is a common GameObject name (backpack, chat, dropdowns) — keep it scoped.
    public readonly Locatable MainScrollView = new(By.PATH, "//Passport(Clone)/BackgroundContainer/Scroll View");

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
    /// The About Me text is edited inline: the edit pencil swaps the read-only InfoField for
    /// the InfoField_EDITION_MODE input plus Save/Cancel buttons, and saving swaps back.
    /// </summary>
    public class AboutMeModule() : BaseView(new(By.NAME, "UserDetailInfo_PassportSubView"))
    {
        #region Elements

        public readonly Readable  AboutMeTitle            = new(By.PATH, "//UserDetailInfo_PassportSubView/InfoTitle");
        public readonly Clickable EditAboutMeButton       = new(By.NAME, "Info_Button_Edit");
        public readonly Readable  BioText                 = new(By.PATH, "//UserDetailInfo_PassportSubView/InfoField");
        public readonly Writable  BioInput                = new(By.PATH, "//UserDetailInfo_PassportSubView//InfoField_EDITION_MODE");
        public readonly Clickable SaveBioButton           = new(By.PATH, "//UserDetailInfo_PassportSubView//SaveInfoButton");
        public readonly Clickable CancelBioButton         = new(By.PATH, "//UserDetailInfo_PassportSubView//CancelInfoButton");
        public readonly Locatable AdditionalInfoContainer = new(By.PATH, "//UserDetailInfo_PassportSubView/AdditionalInfoContainer");
        public readonly Locatable AdditionalFieldItem     = new(By.NAME, "AdditionalField_PassportField(Clone)");
        public readonly Readable  LinksTitle              = new(By.NAME, "LinksTitle");
        public readonly Clickable EditLinksButton         = new(By.NAME, "Links_Button_Edit");
        public readonly Locatable LinksContainer          = new(By.NAME, "LinksContainer");
        public readonly Clickable LinkItem                = new(By.NAME, "Link_PassportField(Clone)");

        #endregion

        #region Helper methods

        /// <summary>
        /// Sets the About Me bio through the inline edit mode and waits for the save to
        /// complete (the read-only InfoField re-enables once the profile update lands).
        /// </summary>
        [AllureStep("Set the About Me bio")]
        public void SetBio(string bio)
        {
            EditAboutMeButton.Click();
            // Edit mode swaps the read-only InfoField for the input asynchronously. Wait for
            // the input explicitly instead of leaning on SetText's own (shorter) wait, so a
            // slow swap reports the missing input rather than a nested invocation exception.
            BioInput.WaitFor(SlowChassis.SETTLE_TIMEOUT, verificationShot: false);
            BioInput.SetText(bio, submit: false);
            SaveBioButton.Click();
            // The read-only field comes back only once the profile update round-trip lands.
            BioText.WaitFor(SlowChassis.SETTLE_TIMEOUT);
            Reporter.Log($"Saved About Me bio '{bio}'");
        }

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
