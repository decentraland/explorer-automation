namespace ExplorerAutomation.Tests.Tests.Visual;

/// <summary>
/// Suite-level lifecycle for visual fixtures. NUnit's [SetUpFixture] is scoped to the
/// namespace it lives in, so this only runs when at least one test under
/// ExplorerAutomation.Tests.Tests.Visual is selected — auth/inworld runs are unaffected.
///
/// Today the host-server lifecycle is owned by metaforge (`mf explorer server start/stop`),
/// not this fixture. We fail fast with a clear message when:
///   1. The visual run was invoked without orchestration that injects VISUAL_HOST_URL.
///   2. The Explorer framebuffer is captured at other than the platform-expected size — without
///      this guard, every fixture's OneTimeSetUp loads a scene (slow) before Snapshot.AssertFrameSize
///      trips with the same root cause, multiplying the wait by however many fixtures are selected.
/// A transient capture failure only logs and continues — per-test enforcement still lives in
/// Snapshot.AssertFrameSize and AssertSizeMatchesBaseline, so the suite probe must never abort
/// the namespace over a capture fault.
/// </summary>
[SetUpFixture]
public class VisualSuiteSetup
{
    // Canonical capture resolution — see Snapshot for the full rationale.
    // Mac: 960x540 (MetaForge default). Windows: 1280x720 (Hyper-V vGPU on the GPU runner won't
    // honor 960x540; pipeline injects `--resolution 1280x720` into the metaforge CLI).
    private static readonly int EXPECTED_FRAME_WIDTH = OperatingSystem.IsWindows() ? 1280 : 960;
    private static readonly int EXPECTED_FRAME_HEIGHT = OperatingSystem.IsWindows() ? 720 : 540;

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
        // A transient capture failure (empty or undecodable frame, including AltTester
        // driver/socket faults) must not abort the namespace: log it and let the
        // per-test checks in Snapshot enforce.
        int actualW, actualH;
        byte[] wrongSizePng;
        try
        {
            using var bmp = ScreenshotCapture.CaptureBitmap(quality: 100);

            if (bmp.Width == EXPECTED_FRAME_WIDTH && bmp.Height == EXPECTED_FRAME_HEIGHT)
            {
                Reporter.Log($"VisualSuiteSetup: framebuffer {bmp.Width}x{bmp.Height} OK.");
                return;
            }

            actualW = bmp.Width;
            actualH = bmp.Height;
            wrongSizePng = ScreenshotCapture.EncodePng(bmp);
        }
        catch (Exception ex)
        {
            Reporter.Log($"VisualSuiteSetup: framebuffer probe failed ({ex.Message}). Continuing.");
            return;
        }

        Reporter.AttachPng("visual-suite.wrong-size", wrongSizePng);

        throw new InvalidOperationException(
            $"Visual suite aborted: captured framebuffer is {actualW}x{actualH}, " +
            $"expected {EXPECTED_FRAME_WIDTH}x{EXPECTED_FRAME_HEIGHT}.\n\n" +
            "Defaults are 960x540 on Mac and 1280x720 on Windows. A drift means either an older " +
            "MetaForge is in use (check `mf --version`), or the wrong `--resolution WxH` was " +
            "passed without bumping the baselines.");
    }
}
