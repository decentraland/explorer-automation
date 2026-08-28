namespace ExplorerAutomation.Tests.Views;

/// <summary>
/// View for the minimap HUD widget in the top-left corner of the in-world screen.
/// Shows the circular map render with compass letters, the current place name and
/// parcel coordinates, the favorite heart, the context-menu (kebab) button and the
/// collapse/expand chevrons. Lives at //BodyUI/Minimap on build dev_b97439fc.
/// Persistent-layer client view (Shown for the whole session) — carries no ViewName;
/// this view's own waits stay on object presence.
/// </summary>
public class MinimapView() : BaseView(new(By.PATH, "//BodyUI/Minimap"))
{
    #region Elements

    public readonly Readable  PlaceName         = new(By.PATH, "//BodyUI/Minimap//PlaceName");
    public readonly Readable  PlaceCoordinates  = new(By.PATH, "//BodyUI/Minimap//PlaceCoordinates");

    // The circular map render. Clicking it opens the full navmap (ExplorePanel Map tab).
    public readonly Clickable MapRenderButton   = new(By.PATH, "//BodyUI/Minimap//MinimapRendererButton");

    // Compass letters drawn on the map render ring — presence proves the map is rendering.
    // They live under MapRendererContainer/MapRendererTargetImage (not the renderer button).
    public readonly Locatable CompassNorth      = new(By.PATH, "//BodyUI/Minimap//MapRendererTargetImage/N");

    // ToggleButtonWithDisabledState: the child ImageFill is active only while the current
    // place is favorited, so it doubles as the favorite-state readout.
    public readonly Clickable FavoriteButton    = new(By.PATH, "//BodyUI/Minimap//FavoriteButton");
    public readonly Locatable FavoriteHeartFill = new(By.PATH, "//BodyUI/Minimap//FavoriteButton/ImageFill");

    // Kebab button — opens the scene context menu (Set as Home / Copy Link / Reload Scene).
    public readonly Clickable ContextMenuButton = new(By.PATH, "//BodyUI/Minimap//ContextMenuButton");

    // Collapse/Expand chevrons swap enabled state: only one of the pair is active at a time.
    public readonly Clickable CollapseButton    = new(By.PATH, "//BodyUI/Minimap//Collapse");
    public readonly Clickable ExpandButton      = new(By.PATH, "//BodyUI/Minimap//ExpandButton");

    #endregion

    #region Views

    public ContextMenuPopup ContextMenu { get; } = new();

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

    #endregion
}
