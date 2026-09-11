namespace ExplorerAutomation.Tests.Tests.Visual;

/// <summary>
/// Suite-level lifecycle for visual fixtures. NUnit's [SetUpFixture] is scoped to the
/// namespace it lives in, so this only runs when at least one test under
/// ExplorerAutomation.Tests.Tests.Visual is selected — auth/inworld runs are unaffected.
///
/// Today the host-server lifecycle is owned by metaforge (`mf explorer server start/stop`),
/// not this fixture. It fails fast with a clear message when the visual run was invoked
/// without orchestration that injects VISUAL_HOST_URL, hides the local-scene debug menu so
/// it stays out of every baseline, and logs the framebuffer size once so a resolution drift
/// is visible at the top of the report rather than only in whichever fixture happens to
/// snapshot first. Frame size is not asserted here — the authoritative check is
/// Snapshot.AssertSizeMatchesBaseline, which compares against each baseline's own dimensions.
/// </summary>
[SetUpFixture]
public class VisualSuiteSetup
{
    private const string DEBUG_MENU_OBJECT     = "DebugMenuUIDocument(Clone)";
    private const string UI_DOCUMENT_COMPONENT = "UnityEngine.UIElements.UIDocument";
    private const string UI_DOCUMENT_ASSEMBLY  = "UnityEngine.UIElementsModule";
    private const string TRACKER_COMPONENT     = "DCL.UI.UIDocumentTracker";
    private const string TRACKER_ASSEMBLY      = "UI";
    private const double DEBUG_MENU_TIMEOUT    = 15D;
    private const double WORLD_TIMEOUT         = 180D;

    [OneTimeSetUp]
    public void RequireHost()
    {
        var url = Environment.GetEnvironmentVariable("VISUAL_HOST_URL");
        if (string.IsNullOrWhiteSpace(url))
            throw new InvalidOperationException(
                "Visual tests require a running host server but VISUAL_HOST_URL is not set.\n\n" +
                "Run them via metaforge so it can resolve the server and inject the env:\n" +
                "  metaforge explorer server start\n" +
                "  metaforge explorer test --filter \"Category=Visual\"\n\n" +
                "If you're invoking dotnet test directly, set VISUAL_HOST_URL yourself first.");

        Reporter.Log($"VisualSuiteSetup: host = {url}");

        HideLocalSceneDebugMenu();
        LogFrameSize();
    }

    /// <summary>
    /// Switches off the debug menu the client adds whenever local-scene development is on.
    /// </summary>
    /// <remarks>
    /// The visual host server *is* local-scene development, and the client offers no way to
    /// keep one without the other, so the sidebar renders into the top-right of every frame.
    /// Its contents are UIElements rather than GameObjects — the whole document goes, not
    /// individual buttons. Best-effort: a baseline carrying the sidebar is the status quo,
    /// and this is a [SetUpFixture], so throwing would abort the whole Visual namespace.
    /// </remarks>
    private static void HideLocalSceneDebugMenu()
    {
        try
        {
            // Instantiated during world bootstrap, so it is not there while loading is up.
            ViewContainer.Instance.LoadingScreen.WaitForGone(WORLD_TIMEOUT, verificationShot: false);

            var menu = new Locatable(By.NAME, DEBUG_MENU_OBJECT)
                .WaitFor(DEBUG_MENU_TIMEOUT, verificationShot: false);

            // Order is load-bearing. UpdateShowHideUIInputSystem dereferences rootVisualElement
            // for every tracked document each frame, and disabling the UIDocument nulls it, so
            // the tracker has to deregister first or the client throws on every frame after.
            menu.SetComponentProperty(TRACKER_COMPONENT, "enabled", false, TRACKER_ASSEMBLY);
            menu.SetComponentProperty(UI_DOCUMENT_COMPONENT, "enabled", false, UI_DOCUMENT_ASSEMBLY);

            Reporter.Log("VisualSuiteSetup: local-scene debug menu hidden.");
        }
        catch (Exception ex)
        {
            Reporter.Log(
                $"VisualSuiteSetup: could not hide the local-scene debug menu ({ex.Message}). " +
                "Baselines will include it.");
        }
    }

    private static void LogFrameSize()
    {
        // Informational only — enforcement lives in Snapshot.AssertSizeMatchesBaseline.
        // AltDriver is up by now (GlobalSetup ran first) and Unity has applied its launch
        // resolution, so this probes the captured framebuffer size before any Visual scene
        // loads. Must never throw: this is a [SetUpFixture], so a transient capture failure
        // (empty or undecodable frame, including AltTester driver/socket faults) would
        // otherwise abort the whole Visual namespace over a log line.
        try
        {
            using var bmp = ScreenshotCapture.CaptureBitmap(quality: 100);
            Reporter.Log($"VisualSuiteSetup: framebuffer {bmp.Width}x{bmp.Height}");
        }
        catch (Exception ex)
        {
            Reporter.Log($"VisualSuiteSetup: framebuffer probe failed ({ex.Message}). Continuing.");
        }
    }
}
