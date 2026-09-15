using ExplorerAutomation.Tests.Common.SyntheticInput;

namespace ExplorerAutomation.Tests.Common;

/// <summary>
/// Puts the client where a fixture needs it, through
/// <c>DCL.SyntheticInput.AltTester.NavigationAutomationProbe</c>: world/realm changes and parcel
/// teleports take the client's own <c>/goto</c> path, loading screen included, and return once the
/// destination scene is ready. The environment (org/zone) is launch-only — it can be read here but
/// only <c>--dclenv</c> sets it.
/// </summary>
public static class Navigation
{
    public const string PROBE_TYPE = "DCL.SyntheticInput.AltTester.NavigationAutomationProbe";

    private const string WORLD_SUFFIX = ".dcl.eth";

    // A realm change downloads the world's about + scene before the loading screen lifts; the probe
    // times out on its own at this ceiling and the poll sits just above.
    private const double GOTO_TIMEOUT_SECONDS = 120;
    private const double POLL_MARGIN_SECONDS = 10;

    public static bool IsReady() =>
        CommonStuff.AltDriver.CallStaticMethod<bool>(PROBE_TYPE, "IsReady", ProbeOperation.PROBE_ASSEMBLY, new object[] { });

    public static NavigationStatus Status()
    {
        var result = ProbeOperation.Call(PROBE_TYPE, "GetStatusJson", new object[] { }, "read navigation status").RequireOk();

        string sceneName = null;
        var sceneReady = false;

        if (result.Root.TryGetProperty("scene", out var scene) && scene.ValueKind == System.Text.Json.JsonValueKind.Object)
        {
            sceneName = scene.GetProperty("name").GetString();
            sceneReady = scene.GetProperty("ready").GetBoolean();
        }

        return new NavigationStatus(
            result.String("environment"),
            result.String("realmName"),
            result.String("hostname"),
            result.String("realmKind"),
            result.Parcel("currentParcel"),
            result.Bool("loadingScreenOn"),
            sceneName,
            sceneReady);
    }

    /// <summary>
    /// Makes sure the client stands on <paramref name="parcel"/> of <paramref name="world"/> with the scene
    /// there ready — a no-op when it already does, a realm change (or, on the same world, a teleport)
    /// otherwise. <paramref name="world"/> is a world name, with or without the <c>.dcl.eth</c> suffix.
    /// </summary>
    public static void EnsureWorld(string world, Parcel parcel)
    {
        var status = Status();

        if (status.IsOnWorld(world) && status.CurrentParcel == parcel && status.SceneReady && !status.LoadingScreenOn)
        {
            Reporter.Log($"Already on {world} at {parcel} with the scene ready — nothing to do");
            return;
        }

        Reporter.Log($"Navigating to {world} at {parcel} (currently {status})");

        var result = ProbeOperation.Run(PROBE_TYPE, "StartGoToWorld", new object[] { world, parcel.X, parcel.Y, (float)GOTO_TIMEOUT_SECONDS },
            GOTO_TIMEOUT_SECONDS + POLL_MARGIN_SECONDS, $"go to {world} at {parcel}").RequireOk();

        Reporter.Log($"Arrived: {result}");
        WaitForHud();
    }

    /// <summary>Teleports to a parcel of the current realm and waits for the scene there to be ready.</summary>
    public static void Teleport(Parcel parcel)
    {
        Reporter.Log($"Teleporting to {parcel}");

        var result = ProbeOperation.Run(PROBE_TYPE, "StartTeleport", new object[] { parcel.X, parcel.Y, (float)GOTO_TIMEOUT_SECONDS },
            GOTO_TIMEOUT_SECONDS + POLL_MARGIN_SECONDS, $"teleport to {parcel}").RequireOk();

        Reporter.Log($"Arrived: {result}");
        WaitForHud();
    }

    /// <summary>Whether <paramref name="world"/> names this realm, with or without the <c>.dcl.eth</c> suffix.</summary>
    public static bool IsOnWorld(this NavigationStatus status, string world)
    {
        if (string.IsNullOrEmpty(status.RealmName)) return false;

        var full = world.EndsWith(WORLD_SUFFIX, StringComparison.OrdinalIgnoreCase) ? world : world + WORLD_SUFFIX;
        return string.Equals(status.RealmName, full, StringComparison.OrdinalIgnoreCase)
               || string.Equals(status.RealmName, world, StringComparison.OrdinalIgnoreCase);
    }

    // The probe returns once the loading screen is down and the scene is ready; the HUD re-appears a
    // moment later, and every test starts by pressing Escape against it.
    private static void WaitForHud() =>
        ViewContainer.Instance.MainMenu.WaitFor(SlowChassis.SETTLE_TIMEOUT, verificationShot: false);
}

public sealed record NavigationStatus(string Environment, string RealmName, string Hostname, string RealmKind,
    Parcel CurrentParcel, bool LoadingScreenOn, string SceneName, bool SceneReady)
{
    public override string ToString() =>
        $"env {Environment}, realm '{RealmName}' ({RealmKind}), parcel {CurrentParcel}, scene '{SceneName ?? "none"}' ready={SceneReady}, loading={LoadingScreenOn}";
}
