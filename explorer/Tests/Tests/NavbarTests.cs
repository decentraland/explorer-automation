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
    //
    // Seven of them are flag-gated, so their expected state is read from the client rather than
    // assumed — see FeatureFlags. `SidebarController.OnViewInstantiated` deactivates each of these
    // GameObjects when its gate is off, and an inactive object is not findable, so a hard-coded
    // "always present" here fails on every run for as long as the flag stays off.
    [Test]
    public void TestSidebarShowsAllNavigationButtons()
    {
        Views.MainMenu.WaitFor();

        var buttons = new (string Label, Locatable Element, FeatureFlags.Expected Expected)[]
        {
            ("My profile", Views.MainMenu.ProfileButton, FeatureFlags.Expected.Present),
            ("Notifications", Views.MainMenu.NotificationsButton, FeatureFlags.Expected.Present),
            ("Marketplace Credits", Views.MainMenu.MarketplaceCreditsButton, FeatureFlags.UserGated("alfa-marketplace-credits")),
            ("Events", Views.MainMenu.EventsButton, FeatureFlags.Feature("Discover")),
            ("Places", Views.MainMenu.PlacesButton, FeatureFlags.Feature("Discover")),
            ("Communities", Views.MainMenu.CommunitiesButton, FeatureFlags.UserGated("alfa-communities")),
            ("Backpack", Views.MainMenu.BackpackButton, FeatureFlags.Expected.Present),
            ("Marketplace", Views.MainMenu.MarketplaceButton, FeatureFlags.Expected.Present),
            ("Gallery", Views.MainMenu.GalleryButton, FeatureFlags.Feature("CameraReel")),
            ("Settings", Views.MainMenu.SettingsButton, FeatureFlags.Expected.Present),
            ("Help", Views.MainMenu.HelpButton, FeatureFlags.Expected.Present),
            ("Sidebar config", Views.MainMenu.SidebarSettingsButton, FeatureFlags.Expected.Present),
            ("Nearby voice chat", Views.MainMenu.NearbyVoiceButton, FeatureFlags.Feature("NearbyVoiceChat")),
            ("Portable Experiences (smart wearables)", Views.MainMenu.SmartWearablesButton, FeatureFlags.Expected.Present),
            ("Skybox", Views.MainMenu.SkyboxButton, FeatureFlags.Expected.Present),
            ("Camera", Views.MainMenu.InWorldCameraButton, FeatureFlags.Feature("CameraReel")),
            ("Emotes", Views.MainMenu.EmoteWheelButton, FeatureFlags.Expected.Present),
            ("Friends", Views.MainMenu.FriendsButton, FeatureFlags.Feature("Friends")),
            ("Chat", Views.MainMenu.ChatButton, FeatureFlags.Expected.Present),
        };

        var wrong = new List<string>();
        foreach (var (label, element, expected) in buttons)
        {
            if (expected == FeatureFlags.Expected.Unknown)
            {
                Reporter.Log($"Sidebar button not asserted: {label} — flag is on but carries a wallets allowlist");
                continue;
            }

            bool shouldBePresent = expected == FeatureFlags.Expected.Present;
            bool present = element.IsPresent();

            if (present == shouldBePresent)
                Reporter.Log($"Sidebar button {(present ? "present" : "absent, as its flag is off")}: {label}");
            else
                wrong.Add($"{label} (expected {(shouldBePresent ? "present, absent" : "absent, present")})");
        }

        Reporter.Log("Absent from this build's sidebar by design: Map (use M shortcut / Map tab), " +
                     "Keyboard shortcuts (use help menu / H shortcut)");

        Assert.That(wrong, Is.Empty,
            $"Sidebar buttons in a state the client's flags don't call for: {string.Join(", ", wrong)}");
    }

    [Test]
    public void TestOpenAndCloseNotificationsPanel()
    {
        Views.MainMenu.NotificationsButton.Click(settleMs: 0);
        Views.MainMenu.Notifications.WaitFor();

        Assert.That(Views.MainMenu.Notifications.TitleLabel.GetText(), Is.EqualTo("NOTIFICATIONS"),
            "Notifications panel should show its NOTIFICATIONS header");
        Reporter.Log("Notifications panel opened from the sidebar bell");

        // The bell toggles: a second click closes the panel again.
        Views.MainMenu.NotificationsButton.Click(settleMs: 0);
        Views.MainMenu.Notifications.WaitForGone();
        Reporter.Log("Notifications panel closed by clicking the bell again");
    }

    [Test]
    public void TestOpenHelpMenu()
    {
        Views.MainMenu.HelpButton.Click(settleMs: 0);
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
        Views.MainMenu.HelpButton.Click(settleMs: 0);
        Views.MainMenu.Help.WaitFor();

        // The context menu's show animation eats clicks right after the view becomes findable,
        // and there is no raycaster signal here — so click again if the panel never opened.
        // Per-attempt budget matches the WaitFor below, so only a swallowed click is retried.
        ClickUntil(() => Views.MainMenu.Help.MouseAndKeyControlsButton.Click(settleMs: 0),
            () => Views.ControlsPanel.IsPresent(verificationShot: false),
            attempts: 2, timeoutPerAttempt: 20);
        Views.ControlsPanel.WaitFor();
        Reporter.Log("Mouse and Key Controls panel opened from the help menu");

        // Same guard on the panel's own exit button.
        ClickUntil(() => Views.ControlsPanel.ExitButton.Click(settleMs: 0),
            () => !Views.ControlsPanel.IsPresent(verificationShot: false),
            attempts: 2, timeoutPerAttempt: 20);
        Views.ControlsPanel.WaitForGone();
        Reporter.Log("Controls panel closed via its exit button");
    }
}
