namespace ExplorerAutomation.Tests.Tests.Visual;

/// <summary>
/// Suite-level lifecycle for visual fixtures. NUnit's [SetUpFixture] is scoped to the
/// namespace it lives in, so this only runs when at least one test under
/// ExplorerAutomation.Tests.Tests.Visual is selected — auth/inworld runs are unaffected.
///
/// Today the host-server lifecycle is owned by metaforge (`mf explorer server start/stop`),
/// not this fixture. We fail fast with a clear message when:
///   1. The visual run was invoked without orchestration that injects VISUAL_HOST_URL.
///   2. The Explorer framebuffer is not 960x540 — without this guard, every fixture's
///      OneTimeSetUp loads a scene (slow) before Snapshot.AssertFrameSize trips with the
///      same root cause, multiplying the wait by however many fixtures are selected.
/// </summary>
[SetUpFixture]
public class VisualSuiteSetup
{
    // Canonical capture resolution — see Snapshot.AssertFrameSize for the full rationale.
    // MetaForge v2.10.3+ defaults Visual runs to 960x540 (16:9, matches Unity's own toon-shader
    // test projects; sits in the middle of Unity-Technologies SRPTests' 512²→1024×576 range).
    // Both platforms now baseline at the same size.
    private const int EXPECTED_FRAME_WIDTH = 960;
    private const int EXPECTED_FRAME_HEIGHT = 540;

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
            "MetaForge v2.10.3+ defaults Visual runs to 960x540. A drift means either an older " +
            "MetaForge is in use (check `mf --version`), or an explicit `--resolution WxH` was " +
            "passed without bumping the baselines.");
    }
}
