namespace ExplorerAutomation.Tests.Common;

/// <summary>
/// The client's screen size, and the precondition that the UI the suite drives fits inside it.
/// A press outside the viewport never reaches the UI, and AltTester still reports it as a
/// successful click — so without this check a badly shaped host turns every affordance near the
/// top of a panel into an unrelated-looking timeout somewhere further down the test.
/// </summary>
public static class Viewport
{
    // Every full-screen panel's canvas scales with width against a 1920x1080 reference
    // (CanvasScaler, match = 0), so vertical room is decided by the aspect ratio and not by the
    // pixel height: 1024x768 leaves the layout 1440 units to work with, 1920x800 only 800.
    private const float REFERENCE_WIDTH = 1920f;

    /// <summary>
    /// Least vertical room the suite can drive, in the 1080-unit space panels are laid out in.
    /// The passport's close button and edit pencils sit 417 units above centre, so they leave the
    /// screen below ~875; this rounds up. macOS CI gets 942, the Windows runner 800.
    /// </summary>
    public const int MIN_CANVAS_HEIGHT = 900;

    private static bool _logged;

    /// <summary>The client's current screen size, in pixels.</summary>
    public static AltVector2 Size => CommonStuff.AltDriver.GetApplicationScreenSize();

    /// <summary>Throws when the host gives the client a viewport too letterboxed to click through.</summary>
    public static void RequireUsable()
    {
        var size = Size;
        var canvasHeight = REFERENCE_WIDTH * size.y / size.x;

        // Recorded on every run, not just the failing ones: the shape of the screen is the first
        // thing worth knowing when a press lands on nothing.
        if (!_logged)
        {
            _logged = true;
            Reporter.Log($"Client viewport: {size.x:F0}x{size.y:F0} ({canvasHeight:F0} units of UI height)");
        }

        if (canvasHeight >= MIN_CANVAS_HEIGHT) return;

        throw new AssertionException(
            $"Client viewport is {size.x:F0}x{size.y:F0}, leaving the UI {canvasHeight:F0} units of height "
            + $"where the suite needs {MIN_CANVAS_HEIGHT}. Panel headers sit outside a screen this wide for "
            + "its height, and a press outside the screen is dropped. This is the aspect ratio, not the "
            + "pixel size — keep it at or under 2.13:1, or launch the client with --resolution WxH.");
    }
}
