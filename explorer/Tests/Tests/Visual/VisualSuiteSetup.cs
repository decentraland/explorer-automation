namespace ExplorerAutomation.Tests.Tests.Visual;

/// <summary>
/// Suite-level lifecycle for visual fixtures. NUnit's [SetUpFixture] is scoped to the
/// namespace it lives in, so this only runs when at least one test under
/// ExplorerAutomation.Tests.Tests.Visual is selected — auth/inworld runs are unaffected.
///
/// Today the host-server lifecycle is owned by metaforge (`mf explorer server start/stop`),
/// not this fixture. It fails fast with a clear message when the visual run was invoked
/// without orchestration that injects VISUAL_HOST_URL, and logs the framebuffer size once
/// so a resolution drift is visible at the top of the report rather than only in whichever
/// fixture happens to snapshot first. Frame size is not asserted here — the authoritative
/// check is Snapshot.AssertSizeMatchesBaseline, which compares against each baseline's own
/// dimensions.
/// </summary>
[SetUpFixture]
public class VisualSuiteSetup
{
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

        LogFrameSize();
    }

    private static void LogFrameSize()
    {
        // Informational only. AltDriver is up by now (GlobalSetup ran first) and Unity has
        // applied its launch resolution, so this probes Screen.width/height before any
        // Visual scene loads. Enforcement lives in Snapshot.AssertSizeMatchesBaseline.
        using var bmp = ScreenshotCapture.CaptureBitmap(quality: 100);
        Reporter.Log($"VisualSuiteSetup: framebuffer {bmp.Width}x{bmp.Height}");
    }
}
