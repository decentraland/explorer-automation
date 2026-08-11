namespace ExplorerAutomation.Tests.Tests;

[AllureSuite("Navbar Tests")]
[Category("InWorld")]
[Order(18)]
public class NavbarTests : BaseTest
{
    // The navbar checklist lists 19 sidebar entries. Two of them do not exist as sidebar
    // buttons in this dev build (verified via UiDump `--all` dumps on build dev_b97439fc):
    //   - Map (SidebarMapButton) — the navmap is reachable via the M shortcut and the
    //     explore panel's Map tab instead (covered by ShortcutsTests and NavmapTests).
    //   - Keyboard shortcuts (SidebarControlsScreenButton) — the controls panel is reachable
    //     via the help menu / H shortcut instead (covered by TestOpenControlsPanelFromHelpMenu).
    [Test]
    public void TestSidebarShowsAllNavigationButtons()
    {
        Views.MainMenu.WaitFor();

        var buttons = new (string Label, Locatable Element)[]
        {
            ("My profile", Views.MainMenu.ProfileButton),
            ("Notifications", Views.MainMenu.NotificationsButton),
            ("Marketplace Credits", Views.MainMenu.MarketplaceCreditsButton),
            ("Events", Views.MainMenu.EventsButton),
            ("Places", Views.MainMenu.PlacesButton),
            ("Communities", Views.MainMenu.CommunitiesButton),
            ("Backpack", Views.MainMenu.BackpackButton),
            ("Marketplace", Views.MainMenu.MarketplaceButton),
            ("Gallery", Views.MainMenu.GalleryButton),
            ("Settings", Views.MainMenu.SettingsButton),
            ("Help", Views.MainMenu.HelpButton),
            ("Sidebar config", Views.MainMenu.SidebarSettingsButton),
            ("Nearby voice chat", Views.MainMenu.NearbyVoiceButton),
            ("Portable Experiences (smart wearables)", Views.MainMenu.SmartWearablesButton),
            ("Skybox", Views.MainMenu.SkyboxButton),
            ("Camera", Views.MainMenu.InWorldCameraButton),
            ("Emotes", Views.MainMenu.EmoteWheelButton),
            ("Friends", Views.MainMenu.FriendsButton),
            ("Chat", Views.MainMenu.ChatButton),
        };

        var missing = new List<string>();
        foreach (var (label, element) in buttons)
        {
            if (element.IsPresent())
                Reporter.Log($"Sidebar button present: {label}");
            else
                missing.Add(label);
        }

        Reporter.Log("Absent from this build's sidebar by design: Map (use M shortcut / Map tab), " +
                     "Keyboard shortcuts (use help menu / H shortcut)");

        Assert.That(missing, Is.Empty,
            $"Sidebar buttons missing from this build: {string.Join(", ", missing)}");
    }

    [Test]
    public void TestOpenAndCloseNotificationsPanel()
    {
        Views.MainMenu.NotificationsButton.Click();
        Views.MainMenu.Notifications.WaitFor();

        Assert.That(Views.MainMenu.Notifications.TitleLabel.GetText(), Is.EqualTo("NOTIFICATIONS"),
            "Notifications panel should show its NOTIFICATIONS header");
        Reporter.Log("Notifications panel opened from the sidebar bell");

        // The bell toggles: a second click closes the panel again.
        Views.MainMenu.NotificationsButton.Click();
        Views.MainMenu.Notifications.WaitForGone();
        Reporter.Log("Notifications panel closed by clicking the bell again");
    }

    [Test]
    public void TestOpenHelpMenu()
    {
        Views.MainMenu.HelpButton.Click();
        Views.MainMenu.Help.WaitFor();
        Reporter.Log("Help menu opened from the sidebar");

        // FAQ / Contact Support / Discord open the system browser, which would steal focus
        // from the Explorer window and break AltTester input — assert presence only.
        Assert.That(Views.MainMenu.Help.MouseAndKeyControlsButton.IsPresent(), Is.True,
            "Help menu should contain the Mouse and Key Controls entry");
        Assert.That(Views.MainMenu.Help.FaqButton.IsPresent(), Is.True,
            "Help menu should contain the FAQ entry");
        Assert.That(Views.MainMenu.Help.ContactSupportButton.IsPresent(), Is.True,
            "Help menu should contain the Contact Support entry");
        Assert.That(Views.MainMenu.Help.DiscordButton.IsPresent(), Is.True,
            "Help menu should contain the Discord entry");

        PressEscape(delay: 0);
        Views.MainMenu.Help.WaitForGone();
    }

    [Test]
    public void TestOpenControlsPanelFromHelpMenu()
    {
        Views.MainMenu.HelpButton.Click();
        Views.MainMenu.Help.WaitFor();

        // The context menu's show animation eats clicks right after the view becomes findable,
        // and there is no raycaster signal here — so click again if the panel never opened.
        // Per-attempt budget matches the WaitFor below, so only a swallowed click is retried.
        ClickUntil(() => Views.MainMenu.Help.MouseAndKeyControlsButton.Click(),
            () => Views.ControlsPanel.IsPresent(verificationShot: false),
            attempts: 2, timeoutPerAttempt: 20);
        Views.ControlsPanel.WaitFor();
        Reporter.Log("Mouse and Key Controls panel opened from the help menu");

        // Same guard on the panel's own exit button.
        ClickUntil(() => Views.ControlsPanel.ExitButton.Click(),
            () => !Views.ControlsPanel.IsPresent(verificationShot: false),
            attempts: 2, timeoutPerAttempt: 20);
        Views.ControlsPanel.WaitForGone();
        Reporter.Log("Controls panel closed via its exit button");
    }
}
