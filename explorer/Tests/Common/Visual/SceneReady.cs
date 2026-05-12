namespace ExplorerAutomation.Tests.Common.Visual;

/// <summary>
/// Polls <c>SceneRunner.Scene.AlttesterSceneReadinessProbe</c> in the running Explorer until the
/// current scene reports <c>SceneLoadingConcluded == true</c>. Replaces the heuristic
/// <see cref="Frame.WaitForStable"/> pixel-watch for fixtures that just need to know the
/// scene has finished streaming before snapshotting.
///
/// Two-phase wait so hot-reload fixtures don't false-pass against the soon-to-be-disposed
/// previous facade:
///   1. Wait for the probe to observe not-ready (the WS-triggered teardown has cleared the
///      current facade, or the freshly-installed facade hasn't loaded its assets yet).
///   2. Wait for the probe to flip ready.
/// If the not-ready window elapses without ever seeing not-ready, we log and proceed —
/// the reload may have been a no-op (e.g. identical bundle bytes after a same-scene reload).
/// </summary>
public static class SceneReady
{
    private const string PROBE_TYPE = "SceneRunner.Scene.AlttesterSceneReadinessProbe";
    private const string PROBE_ASSEMBLY = "SceneRunner.Scene";

    public static void WaitUntilReady(
        int timeoutMs = 60_000,
        int pollIntervalMs = 250,
        int notReadyWindowMs = 10_000)
    {
        AllureApi.Step($"Wait for scene to cycle through not-ready → ready (timeout {timeoutMs}ms)", () =>
        {
            var startedAt = DateTime.UtcNow;
            var notReadyDeadline = startedAt.AddMilliseconds(notReadyWindowMs);
            var totalDeadline = startedAt.AddMilliseconds(timeoutMs);

            bool sawNotReady = false;
            while (DateTime.UtcNow < notReadyDeadline)
            {
                if (!IsReady())
                {
                    sawNotReady = true;
                    break;
                }
                Thread.Sleep(pollIntervalMs);
            }

            if (!sawNotReady)
                Reporter.Log(
                    $"SceneReady: never observed not-ready within {notReadyWindowMs}ms; " +
                    "reload may have been a no-op, proceeding to ready-check.");

            while (DateTime.UtcNow < totalDeadline)
            {
                if (IsReady())
                {
                    Reporter.Log($"Scene ready: {GetStatusJson()}");
                    return;
                }
                Thread.Sleep(pollIntervalMs);
            }

            throw new AssertionException(
                $"Scene did not report ready within {timeoutMs}ms. Last probe status: {GetStatusJson()}");
        });
    }

    private static bool IsReady() =>
        CommonStuff.AltDriver.CallStaticMethod<bool>(
            PROBE_TYPE, "IsCurrentSceneReady", PROBE_ASSEMBLY, new object[] { });

    private static string GetStatusJson() =>
        CommonStuff.AltDriver.CallStaticMethod<string>(
            PROBE_TYPE, "GetCurrentSceneStatusJson", PROBE_ASSEMBLY, new object[] { });
}
