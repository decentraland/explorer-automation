namespace ExplorerAutomation.Tests.Tests;

// Order 15 duplicates CrossPlatformVerificationTests — the in-world band (10–19) is full,
// and duplicate Order values already have precedent (16, 19). See explorer/README.md.
[AllureSuite("Passport Tests")]
[Category("InWorld")]
[Order(15)]
public class PassportTests : BaseTest
{
    // NameInputFieldView.maxNameLength — the editor's own validity rule, which gates whether its
    // Save button is interactable at all.
    private const int MAX_NAME_LENGTH = 15;

    // Name color edit is NOT covered here: on build dev_b97439fc the NameColorPicker
    // container (parent of ChangeColorButton) stays disabled for the test account because
    // its name is unclaimed — the header shows the Claim Name CTA instead. Verified live
    // via UiDump (--all dump of UserBasicInfo_PassportSubView while the own passport was
    // open). The feature requires a claimed NAME NFT, which this account does not own.

    [Test]
    public void TestOpenOwnPassportFromSidebar()
    {
        OpenOwnPassport();
        Reporter.Log("Own passport opened from the sidebar profile menu");

        Views.Passport.CloseButton.Click(settleMs: 0);
        Views.Passport.WaitForGone();
        Reporter.Log("Passport closed via its close button");
    }

    [Test]
    public void TestPassportOverviewShowsCoreContent()
    {
        OpenOwnPassport();

        var userName = Views.Passport.UserNameText.GetText();
        Assert.That(userName, Is.Not.Empty, "Passport header should display the user name");
        Assert.That(Views.Passport.UserNameHashtagText.GetText(), Does.StartWith("#"),
            "Unclaimed names should display their # discriminator next to the name");

        var userId = Views.Passport.UserIDText.GetText();
        Assert.That(userId, Does.StartWith("0x"),
            "Passport header should display the shortened wallet address");
        Reporter.Log($"Passport header shows '{userName}' with address snippet '{userId}'");

        Views.Passport.AvatarPreviewImage.WaitFor();
        Views.Passport.Overview.WaitFor();
        Assert.That(Views.Passport.Overview.AboutMe.AboutMeTitle.GetText(), Is.EqualTo("ABOUT ME"),
            "Overview should contain the About Me module");
        Assert.That(Views.Passport.Overview.AboutMe.BioText.GetText(), Is.Not.Empty,
            "About Me should display a description (or its 'No intro.' empty state)");
        Assert.That(Views.Passport.Overview.EquippedItems.EquippedItemsTitle.GetText(), Is.EqualTo("EQUIPPED ITEMS"),
            "Overview should contain the Equipped Items module");
        Reporter.Log("Overview shows avatar preview, About Me, and Equipped Items modules");

        Views.Passport.CloseButton.Click(settleMs: 0);
        Views.Passport.WaitForGone();
    }

    [Test]
    public void TestEditUserNameAndRevert()
    {
        OpenOwnPassport();

        // The account's name is unclaimed, so renaming is a plain profile update with no cooldown.
        // Read the current name rather than hardcoding it, so an earlier aborted run can't strand
        // the account under a temp name — but it is not necessarily restorable: the editor keeps
        // Save non-interactable unless the name is at most MAX_NAME_LENGTH, and CI provisions
        // accounts as ciinworld<runId>, which is longer. Establish a valid baseline first, or the
        // revert silently cannot be saved and the modal never closes.
        var originalName = Views.Passport.UserNameText.GetText();
        var originalHashtag = Views.Passport.UserNameHashtagText.GetText();
        var baseName = originalName.Length is > 0 and <= MAX_NAME_LENGTH ? originalName : "devauto";
        if (baseName != originalName)
        {
            Reporter.Log($"'{originalName}' exceeds the {MAX_NAME_LENGTH}-character limit — "
                         + $"renaming to '{baseName}' to get a restorable baseline");
            Views.Passport.RenameUser(baseName);
            Assert.That(Views.Passport.WaitForUserName(baseName), Is.True,
                "Passport header should show the baseline name before the revert is exercised");
        }

        var tempName = baseName == "devauto" ? "devauto2" : "devauto";
        Reporter.Log($"Renaming '{baseName}' to '{tempName}'");

        Views.Passport.RenameUser(tempName);
        Assert.That(Views.Passport.WaitForUserName(tempName), Is.True,
            "Passport header should show the new name after saving the rename");
        Assert.That(Views.Passport.UserNameHashtagText.GetText(), Is.EqualTo(originalHashtag),
            "The # discriminator is address-derived and must not change on rename");

        Views.Passport.RenameUser(baseName);
        Assert.That(Views.Passport.WaitForUserName(baseName), Is.True,
            "Passport header should show the baseline name after reverting");
        Reporter.Log($"Name reverted to '{baseName}'");

        Views.Passport.CloseButton.Click(settleMs: 0);
        Views.Passport.WaitForGone();
    }

    [Test]
    public void TestEditAboutMeAndRestore()
    {
        OpenOwnPassport();

        // The edit-mode input pre-fills with the currently displayed text, and an empty bio
        // displays as "No intro." — so restoring the read value keeps the UI identical even
        // when the underlying description started out empty.
        var originalBio = Views.Passport.Overview.AboutMe.BioText.GetText();
        var tempBio = "Automated passport test bio";
        Reporter.Log($"Replacing bio '{originalBio}' with '{tempBio}'");

        Views.Passport.Overview.AboutMe.SetBio(tempBio);
        Assert.That(Views.Passport.Overview.AboutMe.BioText.GetText(), Is.EqualTo(tempBio),
            "About Me should display the new bio after saving");

        // Close and reopen the passport to verify the bio persisted past the edit session.
        Views.Passport.CloseButton.Click(settleMs: 0);
        Views.Passport.WaitForGone();
        OpenOwnPassport();
        Assert.That(Views.Passport.Overview.AboutMe.BioText.GetText(), Is.EqualTo(tempBio),
            "The saved bio should persist after closing and reopening the passport");
        Reporter.Log("Bio persisted across passport close/reopen");

        Views.Passport.Overview.AboutMe.SetBio(originalBio);
        Assert.That(Views.Passport.Overview.AboutMe.BioText.GetText(), Is.EqualTo(originalBio),
            "About Me should display the original bio after restoring");
        Reporter.Log($"Bio restored to '{originalBio}'");

        Views.Passport.CloseButton.Click(settleMs: 0);
        Views.Passport.WaitForGone();
    }

    [Test]
    public void TestEquippedItemsShownInPassport()
    {
        OpenOwnPassport();

        Views.Passport.Overview.EquippedItems.WaitFor();
        Assert.That(Views.Passport.Overview.EquippedItems.EquippedItemsTitle.GetText(), Is.EqualTo("EQUIPPED ITEMS"),
            "Equipped Items module should show its title");

        // Backpack fixtures change which wearables are equipped, so assert that equipped
        // item tiles are present rather than pinning specific items.
        Views.Passport.Overview.EquippedItems.EquippedItemSlot.WaitFor();
        Assert.That(Views.Passport.Overview.EquippedItems.EquippedItemSlot.IsPresent(), Is.True,
            "At least one equipped wearable tile should be shown in the passport");
        Reporter.Log("Equipped items grid shows the currently equipped wearables");

        Views.Passport.CloseButton.Click(settleMs: 0);
        Views.Passport.WaitForGone();
    }

    private void OpenOwnPassport()
    {
        Views.MainMenu.ProfileButton.Click(settleMs: 0);
        // The menu runs its show animation with the raycaster off and eats clicks until it is
        // back on, so wait on the raycaster rather than on the animation's length.
        Views.ProfileMenu.WaitFor().WaitForComponentProperty(
            "UnityEngine.UI.GraphicRaycaster", "enabled", true, "UnityEngine.UI", timeout: 15);
        Views.ProfileMenu.PreviewProfileButton.Click(settleMs: 0);
        Views.Passport.WaitUntilReady();
    }
}
