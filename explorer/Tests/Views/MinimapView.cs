namespace ExplorerAutomation.Tests.Views;

/// <summary>
/// View for the minimap panel the sidebar Map button toggles: map render, place info and skybox controls.
/// Persistent-layer client view — carries no ViewName; waits stay on object presence.
/// </summary>
public class MinimapView() : BaseView(new(By.PATH, "//SidebarView//Minimap"))
{
    #region Elements

    public readonly Readable  PlaceName         = new(By.PATH, "//SidebarView//Minimap//PlaceName");
    public readonly Readable  PlaceCoordinates  = new(By.PATH, "//SidebarView//Minimap//PlaceCoordinates");

    // The circular map render. Clicking it opens the full navmap (ExplorePanel Map tab).
    public readonly Clickable MapRenderButton   = new(By.PATH, "//SidebarView//Minimap//MinimapRendererButton");

    // Compass letters drawn on the map render ring — presence proves the map is rendering.
    // They live under MapRendererContainer/MapRendererTargetImage (not the renderer button).
    public readonly Locatable CompassNorth      = new(By.PATH, "//SidebarView//Minimap//MapRendererTargetImage/N");

    // ToggleButtonWithDisabledState: the child ImageFill is active only while the current
    // place is favorited, so it doubles as the favorite-state readout.
    public readonly Clickable FavoriteButton    = new(By.PATH, "//SidebarView//Minimap//FavoriteButton");
    public readonly Locatable FavoriteHeartFill = new(By.PATH, "//SidebarView//Minimap//FavoriteButton/ImageFill");

    // Kebab button — opens the scene context menu (Set as Home / Copy Link / Reload Scene).
    public readonly Clickable ContextMenuButton = new(By.PATH, "//SidebarView//Minimap//ContextMenuButton");

    // Collapse/Expand chevrons swap enabled state: only one of the pair is active at a time.
    public readonly Clickable CollapseButton    = new(By.PATH, "//SidebarView//Minimap//Collapse");
    public readonly Clickable ExpandButton      = new(By.PATH, "//SidebarView//Minimap//ExpandButton");

    // Toggles the skybox widget; it is not an MVC view, so Escape does not close it.
    public readonly Clickable SkyboxButton      = new(By.PATH, "//SidebarView//Minimap//SidebarSkyboxButton");

    #endregion

    #region Views

    public ContextMenuPopup ContextMenu { get; } = new();
    public SkyboxWidget Skybox { get; } = new();

    #endregion

    #region Helper methods

    /// <summary>
    /// Opens the minimap context menu. ContextMenuButton is a HoverableButton whose
    /// Button.onClick does not fire reliably on AltTester's synthetic Click event on this
    /// build (verified live: Click opened the menu only once per fresh driver session,
    /// while Tap — pointer down/up — opens it every time), so this taps instead.
    /// </summary>
    [AllureStep("Open the minimap context menu")]
    public void OpenContextMenu()
    {
        // Shot-suppressed wait: opening the menu is an action, not a verification.
        var button = ContextMenuButton.WaitFor(20D, verificationShot: false);
        button.Tap();
        Thread.Sleep(500);

        // The tap can occasionally be eaten by the hover tooltip — retry once before failing.
        if (!ContextMenu.IsPresent(verificationShot: false))
        {
            Reporter.Log("Context menu did not open on first tap — retrying");
            button.Tap();
        }

        ContextMenu.WaitFor(10);
        Reporter.Log("Minimap context menu opened");
    }

    /// <summary>
    /// Opens the skybox widget, leaving it as is when already open.
    /// </summary>
    [AllureStep("Open the minimap skybox widget")]
    public void OpenSkybox()
    {
        // Escape does not close it, so a failed test can leave it open; the button toggles.
        if (!Skybox.IsPresent(verificationShot: false))
            SkyboxButton.Click(settleMs: 0);
        Skybox.WaitFor();
    }

    /// <summary>
    /// Closes the skybox widget with its toggle button.
    /// </summary>
    [AllureStep("Close the minimap skybox widget")]
    public void CloseSkybox()
    {
        SkyboxButton.Click(settleMs: 0);
        Skybox.WaitForGone();
    }

    /// <summary>
    /// Whether the current place is favorited (the heart fill child is active).
    /// </summary>
    public bool IsFavorited() => FavoriteHeartFill.IsPresent();

    /// <summary>
    /// Clicks the favorite heart and waits until the heart-fill state actually flips —
    /// the fill only toggles once the favorites service round-trip completes.
    /// </summary>
    [AllureStep("Toggle the minimap favorite heart")]
    public void ToggleFavorite(double timeout = 10D)
    {
        // Shot-suppressed probes: state polling is control flow, not a verification.
        var before = FavoriteHeartFill.IsPresent(verificationShot: false);
        FavoriteButton.Click(settleMs: 0);

        var deadline = DateTime.UtcNow.AddSeconds(timeout);
        while (DateTime.UtcNow < deadline && FavoriteHeartFill.IsPresent(verificationShot: false) == before)
            Thread.Sleep(250);

        var after = FavoriteHeartFill.IsPresent(verificationShot: false);
        if (after == before)
            throw new AssertionException($"Favorite heart fill did not change state within {timeout}s (still {(before ? "favorited" : "not favorited")})");
        Reporter.Log($"Favorite toggled: {before} -> {after}");
    }

    #endregion

    #region Sub views

    /// <summary>
    /// The scene context menu opened by the minimap kebab button. It is the scene-root
    /// GenericContextMenu(Clone) popup (the legacy SideMenu object under the minimap is dead
    /// code on this build) — the popup root is only active while the menu is open, and its
    /// pooled entry rows (ToggleWithText / ButtonWithTextAndIcon clones) are rebuilt per open,
    /// so all locators are scoped under the popup root and match enabled objects only.
    /// Escape closes it.
    /// Its client ViewBase, GenericContextMenuView, is one preallocated instance the client
    /// reuses for chat, avatar/community, camera-reel and passport context menus too — Shown/
    /// Hidden there can't identify this popup specifically, so it carries no ViewName; waits
    /// stay on object presence and OpenContextMenu keeps its tap-retry.
    /// </summary>
    public class ContextMenuPopup : BaseView
    {
        #region Elements

        public const int BUTTON_COUNT = 2;

        // "Set as Home" toggle row. Locatable on purpose: clicking it would change the
        // shared test account's home spawn point.
        public readonly Locatable SetAsHomeToggle =
            new(By.PATH, "//GenericContextMenu(Clone)/ControlsContainer/ToggleWithText(Clone)");

        /// <summary>Labels of the button rows (Copy Link / Reload Scene on the minimap menu).</summary>
        public Readable[] ButtonLabels { get; }

        #endregion

        #region Setup

        public ContextMenuPopup() : base(new(By.NAME, "GenericContextMenu(Clone)"))
        {
            ButtonLabels = new Readable[BUTTON_COUNT];
            for (var i = 0; i < BUTTON_COUNT; i++)
                ButtonLabels[i] = new Readable(By.PATH,
                    $"//GenericContextMenu(Clone)/ControlsContainer/ButtonWithTextAndIcon(Clone)[{i}]//Text (TMP)");
        }

        #endregion
    }

    /// <summary>
    /// Skybox day-cycle widget: Auto toggle and time-of-day slider. The slider only reacts with Auto off.
    /// </summary>
    public class SkyboxWidget() : BaseView(new(By.PATH, "//SidebarView//Minimap//SkyboxWidget"))
    {
        #region Elements

        public readonly Readable  TitleLabel            = new(By.PATH, "//Minimap//SkyboxWidget/TimeProgressionTitle");
        public readonly Clickable AutoProgressionToggle = new(By.PATH, "//Minimap//SkyboxWidget/TimeProgressionToggle");
        public readonly Locatable TimeSlider            = new(By.PATH, "//Minimap//SkyboxWidget/TimeSlider");
        // Sits beside the widget, so it stays visible while the widget is closed. Read it with
        // ReadTime: the client writes it through SetText, which leaves the text property stale.
        public readonly Locatable TimeLabel             = new(By.PATH, "//Minimap//SkyboxConfig/Time");

        #endregion

        #region Helper methods

        /// <summary>
        /// Reads the time-of-day label as rendered.
        /// </summary>
        public string ReadTime() =>
            TimeLabel.WaitFor(20D, verificationShot: false).CallComponentMethod<string>(
                "TMPro.TextMeshProUGUI", "GetParsedText", "Unity.TextMeshPro", []);

        /// <summary>
        /// Reads the Auto toggle's state without changing it.
        /// </summary>
        public bool IsAutoProgressionOn() =>
            AutoProgressionToggle.WaitFor(20D, verificationShot: false)
                .GetComponentProperty<bool>("UnityEngine.UI.Toggle", "isOn", "UnityEngine.UI");

        /// <summary>
        /// Ensures the Auto toggle is in the requested state, asserted through Toggle.isOn.
        /// </summary>
        [AllureStep("Set skybox auto time progression")]
        public void SetAutoProgression(bool enabled)
        {
            var isOn = IsAutoProgressionOn();
            if (isOn == enabled)
            {
                Reporter.Log($"Auto time progression already {(enabled ? "on" : "off")}");
                Reporter.TakeVerificationShot($"toggle_{(enabled ? "on" : "off")}_TimeProgressionToggle");
                return;
            }

            AutoProgressionToggle.Click(settleMs: 0);
            AutoProgressionToggle.WaitFor(20D, verificationShot: false).WaitForComponentProperty(
                "UnityEngine.UI.Toggle", "isOn", enabled, "UnityEngine.UI",
                timeout: SlowChassis.SETTLE_TIMEOUT);
            Reporter.TakeVerificationShot($"toggle_{(enabled ? "on" : "off")}_TimeProgressionToggle");
            Reporter.Log($"Auto time progression turned {(enabled ? "on" : "off")}");
        }

        /// <summary>
        /// Sets the time-of-day slider (0..1 over a 24h day) the way a drag does.
        /// </summary>
        [AllureStep("Set skybox time of day")]
        public void SetTimeNormalized(float normalizedTime)
        {
            TimeSlider.WaitFor().SetComponentProperty(
                "UnityEngine.UI.Slider", "value", normalizedTime, "UnityEngine.UI");
            Reporter.Log($"Skybox time slider set to {normalizedTime:F2}");
        }

        #endregion
    }

    #endregion
}
