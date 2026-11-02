namespace ExplorerAutomation.Tests.Tests;

[AllureSuite("Navbar Tests")]
[Category("InWorld")]
[Order(18)]
public class NavbarTests : BaseTest
{
    // Covers entries a fire-and-forget feature check activates after the sidebar shows
    // (Discover → Communities); everything else is gated synchronously in OnViewInstantiated.
    private const double SIDEBAR_BUTTON_PRESENT_TIMEOUT = 10D;

    // Portable Experiences is left out: its button only shows while a smart wearable is running.
    [Test]
    public void TestSidebarShowsAllNavigationButtons()
    {
        Views.MainMenu.WaitFor();

        AssertPresenceMatchesFlags("Sidebar button",
        [
            ("My profile", Views.MainMenu.ProfileButton, FeatureFlags.Expected.Present),
            ("Notifications", Views.MainMenu.NotificationsButton, FeatureFlags.Expected.Present),
            ("Map", Views.MainMenu.MapButton, FeatureFlags.Expected.Present),
            ("Discover", Views.MainMenu.DiscoverButton, FeatureFlags.Expected.Present),
            ("Shop", Views.MainMenu.ShopButton, FeatureFlags.Expected.Present),
            ("System", Views.MainMenu.SettingsButton, FeatureFlags.Expected.Present),
            ("Sidebar config", Views.MainMenu.SidebarSettingsButton, FeatureFlags.Expected.Present),
            ("Emotes", Views.MainMenu.EmotesButton, FeatureFlags.Expected.Present),
            ("Friends", Views.MainMenu.FriendsButton, FeatureFlags.Feature("Friends")),
            ("Chat", Views.MainMenu.ChatButton, FeatureFlags.Expected.Present),
            ("Gallery", Views.MainMenu.GalleryButton, FeatureFlags.Feature("CameraReel")),
            ("Audio", Views.MainMenu.AudioSettingsButton, FeatureFlags.Expected.Present),
            ("Nearby voice chat", Views.MainMenu.NearbyVoiceButton, FeatureFlags.Feature("NearbyVoiceChat")),
        ]);
    }

    [Test]
    public void TestOpenAndCloseNotificationsPanel()
    {
        Views.MainMenu.ClickRailButton(Views.MainMenu.NotificationsButton,
            () => Views.MainMenu.Notifications.IsPresent(verificationShot: false));
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
    public void TestOpenDiscoverMenu()
    {
        Views.MainMenu.OpenSubMenu(Views.MainMenu.DiscoverButton, Views.MainMenu.Discover);
        Reporter.Log("Discover menu opened from the sidebar");

        AssertPresenceMatchesFlags("Discover option",
        [
            ("Places", Views.MainMenu.Discover.PlacesOption, FeatureFlags.Feature("Discover")),
            ("Events", Views.MainMenu.Discover.EventsOption, FeatureFlags.Feature("Discover")),
            ("Communities", Views.MainMenu.Discover.CommunitiesOption, FeatureFlags.UserGated("alfa-communities")),
        ]);

        PressEscape(delay: 0);
        Views.MainMenu.Discover.WaitForGone();
    }

    [Test]
    public void TestOpenShopMenu()
    {
        Views.MainMenu.OpenSubMenu(Views.MainMenu.ShopButton, Views.MainMenu.Shop);
        Reporter.Log("Shop menu opened from the sidebar");

        // Both banners open the system browser, which steals focus from AltTester — presence only.
        Views.MainMenu.Shop.ExploreWearablesBanner.WaitFor();
        Views.MainMenu.Shop.ExploreEmotesBanner.WaitFor();

        // The rail icon toggles the menu closed.
        Views.MainMenu.ShopButton.Click(settleMs: 0);
        Views.MainMenu.Shop.WaitForGone();
    }

    [Test]
    public void TestOpenSystemMenu()
    {
        Views.MainMenu.OpenSubMenu(Views.MainMenu.SettingsButton, Views.MainMenu.System);
        Reporter.Log("System menu opened from the sidebar");

        // FAQ / Contact Support / Discord open the system browser — presence only.
        AssertPresenceMatchesFlags("System option",
        [
            ("Settings", Views.MainMenu.System.SettingsOption, FeatureFlags.Expected.Present),
            ("Controls", Views.MainMenu.System.ControlsOption, FeatureFlags.Expected.Present),
            ("Contact Support", Views.MainMenu.System.ContactSupportOption, FeatureFlags.Expected.Present),
            ("Report Bug", Views.MainMenu.System.ReportBugOption, FeatureFlags.Feature("BugReport")),
            ("Discord", Views.MainMenu.System.DiscordOption, FeatureFlags.Expected.Present),
            ("FAQ", Views.MainMenu.System.FaqOption, FeatureFlags.Expected.Present),
        ]);

        PressEscape(delay: 0);
        Views.MainMenu.System.WaitForGone();
    }

    [Test]
    public void TestOpenControlsPanelFromSystemMenu()
    {
        Views.MainMenu.OpenSubMenu(Views.MainMenu.SettingsButton, Views.MainMenu.System);

        // Retry only a swallowed click; the budget matches the WaitFor below.
        ClickUntil(() => Views.MainMenu.System.ControlsOption.Click(settleMs: 0),
            () => Views.ControlsPanel.IsPresent(verificationShot: false),
            attempts: 2, timeoutPerAttempt: 20);
        Views.ControlsPanel.WaitFor();
        Reporter.Log("Mouse and Key Controls panel opened from the system menu");

        ClickUntil(() => Views.ControlsPanel.ExitButton.Click(settleMs: 0),
            () => !Views.ControlsPanel.IsPresent(verificationShot: false),
            attempts: 2, timeoutPerAttempt: 20);
        Views.ControlsPanel.WaitForGone();
        Reporter.Log("Controls panel closed via its exit button");
    }

    [Test]
    public void TestOpenAudioMenu()
    {
        Views.MainMenu.OpenSubMenu(Views.MainMenu.AudioSettingsButton, Views.MainMenu.Audio);
        Reporter.Log("Audio menu opened from the sidebar");

        Views.MainMenu.Audio.MasterSlider.WaitFor();
        Views.MainMenu.Audio.InWorldSlider.WaitFor();
        Views.MainMenu.Audio.UiSlider.WaitFor();
        Views.MainMenu.Audio.AvatarSlider.WaitFor();
        Reporter.Log("Master, in-world, UI and avatar volume sliders are visible");

        PressEscape(delay: 0);
        Views.MainMenu.Audio.WaitForGone();
    }

    /// <summary>
    /// Asserts each entry is present exactly when the client's flags call for it.
    /// </summary>
    private void AssertPresenceMatchesFlags(string kind,
        (string Label, Locatable Element, FeatureFlags.Expected Expected)[] entries)
    {
        var wrong = new List<string>();
        foreach (var (label, element, expected) in entries)
        {
            if (expected == FeatureFlags.Expected.Unknown)
            {
                var why = FeatureFlags.IsAvailable
                    ? "flag is on but carries a wallets allowlist"
                    : "this build does not expose its flag state";
                Reporter.Log($"{kind} not asserted: {label} — {why}");
                continue;
            }

            bool shouldBePresent = expected == FeatureFlags.Expected.Present;

            // Absent needs no wait: an inactive object is stably unfindable.
            bool present = shouldBePresent
                ? WaitUntil(() => element.IsPresent(verificationShot: false), timeoutSeconds: SIDEBAR_BUTTON_PRESENT_TIMEOUT)
                : element.IsPresent();

            if (shouldBePresent)
                Reporter.TakeVerificationShot($"{(present ? "present" : "absent")}_{element.ShotName}");

            if (present == shouldBePresent)
                Reporter.Log($"{kind} {(present ? "present" : "absent, as its flag is off")}: {label}");
            else
                wrong.Add($"{label} (expected {(shouldBePresent ? "present, absent" : "absent, present")})");
        }

        Assert.That(wrong, Is.Empty,
            $"{kind}s in a state the client's flags don't call for: {string.Join(", ", wrong)}");
    }
}
