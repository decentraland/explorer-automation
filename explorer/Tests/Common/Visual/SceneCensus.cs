namespace ExplorerAutomation.Tests.Common.Visual;

/// <summary>
/// Cheap "what's currently in the scene graph" probes built on top of AltDriver. Used by
/// visual fixtures to log content-readiness signals around the snapshot moment so we can
/// tell apart GPU non-determinism from "scene still streaming when the shutter clicked".
///
/// Every call is wrapped in try/catch — a probe that throws (driver hiccup, type rename in
/// unity-explorer) must not fail the surrounding visual test. Failures are logged and the
/// count falls back to -1 so the diff between record/validate runs stays readable.
/// </summary>
public static class SceneCensus
{
    /// <summary>
    /// Log a one-liner with multiple count signals so we can spot streaming-races in CI logs
    /// without re-running with a debugger. Cheap enough to call multiple times around the
    /// snapshot (e.g. pre/post WaitForStable) — each FindObjects roundtrip is a single WS
    /// call, no per-element overhead.
    /// </summary>
    public static void Log(string label)
    {
        var total = SafeCount(() => CommonStuff.AltDriver.GetAllElements().Count);
        var tmpText = SafeCount(() => CommonStuff.AltDriver.FindObjects(By.COMPONENT, "TMPro.TextMeshPro").Count);
        var meshRenderers = SafeCount(() => CommonStuff.AltDriver.FindObjects(By.COMPONENT, "UnityEngine.MeshRenderer").Count);

        Reporter.Log(
            $"SceneCensus[{label}]: total={total}, tmpText={tmpText}, meshRenderers={meshRenderers}");
    }

    private static int SafeCount(Func<int> probe)
    {
        try
        {
            return probe();
        }
        catch (Exception ex)
        {
            Reporter.Log($"SceneCensus probe failed: {ex.GetType().Name}: {ex.Message}");
            return -1;
        }
    }
}
