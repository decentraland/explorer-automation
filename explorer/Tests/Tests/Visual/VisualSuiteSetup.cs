namespace ExplorerAutomation.Tests.Tests.Visual;

/// <summary>
/// Suite-level lifecycle for visual fixtures. NUnit's [SetUpFixture] is scoped to the
/// namespace it lives in, so this only runs when at least one test under
/// ExplorerAutomation.Tests.Tests.Visual is selected — auth/inworld runs are unaffected.
///
/// Today the host-server lifecycle is owned by metaforge (`mf explorer server start/stop`),
/// not this fixture. We fail fast with a clear message when:
///   1. The visual run was invoked without orchestration that injects VISUAL_HOST_URL.
///   2. The Explorer framebuffer is not the platform-native size (macOS=1920x1080,
///      Windows=1024x768) — without this guard, every fixture's OneTimeSetUp loads a scene
///      (slow) before Snapshot.AssertFrameSize trips with the same root cause, multiplying
///      the wait by however many fixtures are selected.
/// </summary>
[SetUpFixture]
public class VisualSuiteSetup
{
    // Platform-native framebuffer size — see Snapshot.AssertFrameSize for the full rationale.
    // macOS chassis renders at 1920x1080 (honors --resolution against attached display).
    // Windows GH-hosted runner renders at 1024x768 (headless WDDM denies DXGI mode-switch and
    // Unity falls back to FullScreenWindow at the desktop default).
    private static readonly int EXPECTED_FRAME_WIDTH = OperatingSystem.IsWindows() ? 1024 : 1920;
    private static readonly int EXPECTED_FRAME_HEIGHT = OperatingSystem.IsWindows() ? 768 : 1080;

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

        EnsureExpectedFrameSize();
    }

    private static void EnsureExpectedFrameSize()
    {
        // Probe the Explorer framebuffer once at the suite boundary. AltDriver is up by
        // now (GlobalSetup ran first), Unity has applied its launch resolution, and we
        // haven't loaded a Visual scene yet — so this only probes Screen.width/height,
        // not anything scene-specific. If wrong, abort the whole suite instead of
        // letting every fixture pay scene-load cost before failing on the same check.
        using var bmp = ScreenshotCapture.CaptureBitmap(quality: 100);

        if (bmp.Width == EXPECTED_FRAME_WIDTH && bmp.Height == EXPECTED_FRAME_HEIGHT)
        {
            Reporter.Log($"VisualSuiteSetup: framebuffer {bmp.Width}x{bmp.Height} OK.");
            return;
        }

        var wrongSizePng = ScreenshotCapture.EncodePng(bmp);
        Reporter.AttachPng("visual-suite.wrong-size", wrongSizePng);

        throw new InvalidOperationException(
            $"Visual suite aborted: captured framebuffer is {bmp.Width}x{bmp.Height}, " +
            $"expected {EXPECTED_FRAME_WIDTH}x{EXPECTED_FRAME_HEIGHT}.\n\n" +
            "Per-OS native expectations: macOS=1920x1080, Windows=1024x768 (headless WDDM default — " +
            "DXGI denies ExclusiveFullScreen, so Unity falls back to FullScreenWindow at desktop size). " +
            "A drift from these values means something other than the known headless-fallback path broke " +
            "(e.g. desktop resolution changed, virtual display driver was installed, runner SKU changed).");
    }
}
