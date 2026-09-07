using ExplorerAutomation.Tests.Views.PassportSections;

namespace ExplorerAutomation.Tests.Views;

/// <summary>
/// View for the user passport popup that displays a player's profile,
/// with actions (add friend, chat, call) and tabs (overview, badges, photos, creations).
/// The own passport opens from the sidebar profile button via the profile menu's
/// "View PROFILE" entry (<see cref="ProfileMenuView.PreviewProfileButton"/>).
/// </summary>
public class PassportView() : BaseView(new(By.NAME, "Passport(Clone)"))
{
    protected override string ViewName => "PassportView";

    #region Elements

    public readonly Clickable BackgroundCloseButton = new(By.NAME, "Background_Close");

    // Action bar (all of these are disabled on the own passport — only Button_Close applies there)
    public readonly Clickable AddFriendButton = new(By.NAME, "AddFriend");
    public readonly Clickable ChatButton      = new(By.NAME, "ChatButton");
    public readonly Clickable CallButton      = new(By.NAME, "VoiceChatButtonPassport");
    public readonly Clickable MenuButton      = new(By.NAME, "ContextMenuButton");
    public readonly Clickable CloseButton     = new(By.NAME, "Button_Close");

    // Header — paths are scoped to UserBasicInfo_PassportSubView because bare names like
    // "UserName" / "UserName_Hashtag" also match chat conversation rows and the sidebar
    // profile menu (verified via UiDump on build dev_b97439fc).
    public readonly Readable  UserNameText        = new(By.PATH, "//UserBasicInfo_PassportSubView/UserNameContainer/UserName");
    public readonly Readable  UserNameHashtagText = new(By.PATH, "//UserBasicInfo_PassportSubView/UserNameContainer/UserName_Hashtag");
    public readonly Readable  UserIDText          = new(By.PATH, "//UserBasicInfo_PassportSubView/UserIDContainer/UserID");
    public readonly Clickable CopyNameButton      = new(By.PATH, "//UserBasicInfo_PassportSubView/UserNameContainer/CopyButton");
    public readonly Clickable CopyIDButton        = new(By.PATH, "//UserBasicInfo_PassportSubView/UserIDContainer/CopyButton");
    // Bare name, though the pencil does sit under UserBasicInfo_PassportSubView: the module leaves
    // both pencils inactive until the own-profile check resolves, and AltTester skips inactive
    // objects, so a scoped lookup races that and finds nothing.
    public readonly Clickable EditNameButton      = new(By.NAME, "EditNameButton");
    public readonly Clickable ClaimNameButton     = new(By.PATH, "//UserBasicInfo_PassportSubView//ClaimNameButton");

    // Name color picker: the NameColorPicker container stays disabled for an unclaimed name
    // (the test account shows the Claim Name CTA instead), so ChangeColorButton is
    // unreachable on this account — kept for accounts with a claimed NAME.
    public readonly Locatable NameColorPicker       = new(By.PATH, "//UserBasicInfo_PassportSubView//NameColorPicker");
    public readonly Clickable ChangeNameColorButton = new(By.NAME, "ChangeColorButton");

    // Module roots, waited on by WaitUntilReady. The Overview modules carry their own roots as
    // sub-views; the header module has none of its own because its fields are path-scoped to it.
    public readonly Locatable BasicInfoModule = new(By.NAME, "UserBasicInfo_PassportSubView");

    public readonly Locatable OfficialMarkImage  = new(By.PATH, "//UserBasicInfo_PassportSubView/UserNameContainer/OfficialMark");
    public readonly Locatable VerifiedMarkImage  = new(By.PATH, "//UserBasicInfo_PassportSubView/UserNameContainer/VerifiedMark");
    public readonly Locatable AvatarPreviewImage = new(By.NAME, "PreviewRawImage");

    // Tabs
    public readonly Clickable OverviewTab  = new(By.NAME, "OverviewSectionButton");
    public readonly Clickable BadgesTab    = new(By.NAME, "BadgesSectionButton");
    public readonly Clickable PhotosTab    = new(By.NAME, "PhotosSectionButton");
    public readonly Clickable CreationsTab = new(By.NAME, "CreationsSectionButton");

    #endregion

    #region Views

    public PassportOverviewSectionView Overview   { get; } = new();
    public NameEditorModal             NameEditor { get; } = new();

    #endregion

    #region Helper methods

    /// <summary>
    /// Waits until the passport has finished building — the popup, then the module roots —
    /// so a press does not land on a half-built panel.
    /// </summary>
    [AllureStep("Wait for the passport to finish building")]
    public void WaitUntilReady()
    {
        WaitFor(SlowChassis.SETTLE_TIMEOUT, verificationShot: false);
        // Module roots only. Their contents are conditional — an empty badges row and links list
        // swap in placeholder labels — so waiting on a tile hangs for an account that has none.
        // Shots suppressed: one capture of the built panel replaces four near-identical frames.
        BasicInfoModule.WaitFor(SlowChassis.SETTLE_TIMEOUT, verificationShot: false);
        Overview.BadgesOverview.WaitFor(SlowChassis.SETTLE_TIMEOUT, verificationShot: false);
        Overview.AboutMe.WaitFor(SlowChassis.SETTLE_TIMEOUT, verificationShot: false);
        Overview.EquippedItems.WaitFor(SlowChassis.SETTLE_TIMEOUT, verificationShot: false);
        Reporter.TakeVerificationShot("ready_Passport");
    }

    /// <summary>
    /// Renames the user through the passport name editor modal (unclaimed-name flow):
    /// opens the editor, types the new name, saves, and waits for the modal to close.
    /// </summary>
    [AllureStep("Rename user via the passport name editor")]
    public void RenameUser(string newName)
    {
        // No WaitFor on the modal after this: Open only returns once it is present, and it
        // reports a genuine failure better than the stock element error would.
        PassportEditPress.Open(EditNameButton,
            () => NameEditor.IsPresent(verificationShot: false), "the name editor");
        // The modal carries its own SoftMask, so SaveButton is vetoed the same way the pencil was
        // and the editor never closes. ClaimedNameContainer is left alone — it stays inactive for
        // an unclaimed name, so it has no press to veto.
        PassportEditPress.DisableSoftMasks("//ProfileNameEditor(Clone)/NonClaimedNameContainer");
        // The modal pre-fills the input with the current name asynchronously after opening.
        // Typing before that lands gets overwritten by the pre-fill, and Save then submits
        // the unchanged name (verified live) — so wait for the pre-fill first.
        NameEditor.WaitForPrefill();
        NameEditor.NameInput.SetText(newName, submit: false);
        NameEditor.SaveButton.Click(settleMs: 0);
        NameEditor.WaitForGone(SlowChassis.SETTLE_TIMEOUT);
        Reporter.Log($"Saved username '{newName}' via the name editor");
    }

    /// <summary>
    /// Polls the passport header until it displays the expected user name.
    /// The header refreshes a few seconds after the name editor closes (the modal closes
    /// optimistically while the profile update is still in flight).
    /// </summary>
    [AllureStep("Wait for the passport header to show a name")]
    public bool WaitForUserName(string expected, double timeoutSeconds = SlowChassis.SETTLE_TIMEOUT)
    {
        // Shot-suppressed reads inside the poll loop: a capture per iteration would both spam
        // the report and eat the fixed wall-clock deadline (each capture is a synchronous
        // round-trip). The single verification shot is taken once the wait completes, in
        // either outcome, so the report shows the header state the assert ran against.
        var matched = false;
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            if (UserNameText.GetText(20D, verificationShot: false) == expected)
            {
                matched = true;
                break;
            }
            Thread.Sleep(500);
        }

        Reporter.TakeVerificationShot($"{(matched ? "text" : "timeout")}_UserName");
        return matched;
    }

    #endregion

    #region Sub views

    /// <summary>
    /// The "Edit Username" modal (top-level GameObject ProfileNameEditor(Clone)) opened by the
    /// passport's edit-name pencil. Elements below belong to the NonClaimedNameContainer shown
    /// for accounts without a claimed NAME (the test account); the ClaimedNameContainer variant
    /// with its Unique/Non-Unique tabs stays disabled for them.
    /// </summary>
    public class NameEditorModal() : BaseView(new(By.NAME, "ProfileNameEditor(Clone)"))
    {
        #region Elements

        public readonly Writable  NameInput          = new(By.PATH, "//ProfileNameEditor(Clone)//NonClaimedNameContainer//Input");
        public readonly Readable  HashtagText        = new(By.PATH, "//ProfileNameEditor(Clone)//NonClaimedNameContainer//Hash");
        public readonly Readable  CharacterCountText = new(By.PATH, "//ProfileNameEditor(Clone)//NonClaimedNameContainer//CharacterCount");
        public readonly Clickable SaveButton         = new(By.PATH, "//ProfileNameEditor(Clone)//NonClaimedNameContainer//SaveButton");
        public readonly Clickable CancelButton       = new(By.PATH, "//ProfileNameEditor(Clone)//NonClaimedNameContainer//CancelButton");
        public readonly Clickable ClaimNameButton    = new(By.PATH, "//ProfileNameEditor(Clone)//NonClaimedNameContainer//ClaimNameButton");

        #endregion

        #region Helper methods

        /// <summary>
        /// Waits until the modal's asynchronous pre-fill has written the current name into
        /// the input field. Text set before that point gets overwritten by the pre-fill.
        /// </summary>
        [AllureStep("Wait for the name input to pre-fill")]
        public void WaitForPrefill(double timeoutSeconds = SlowChassis.SETTLE_TIMEOUT)
        {
            // Shot-suppressed reads inside the poll loop (see WaitForUserName); one shot at
            // completion shows the pre-filled input the subsequent SetText relies on.
            var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
            while (DateTime.UtcNow < deadline && string.IsNullOrEmpty(NameInput.GetText(10.0f, verificationShot: false)))
                Thread.Sleep(250);

            Reporter.TakeVerificationShot("text_NameEditorInput");
        }

        #endregion
    }

    #endregion
}
