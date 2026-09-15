namespace ExplorerAutomation.Tests.Common.SyntheticInput;

/// <summary>
/// Drives the player through the client's synthetic input layer
/// (<c>DCL.SyntheticInput.AltTester.WorldAutomationProbe</c>): movement, camera look, entity clicks and
/// SDK input actions go through the production input pipelines, so collisions, scene input locks and
/// the CRDT write-back are the real ones. The layer is installed only when the client was launched
/// with <c>--alttester</c> (or <c>--mcp</c>) — <see cref="IsReady"/> says whether it is.
/// </summary>
public static class WorldInput
{
    public const string PROBE_TYPE = "DCL.SyntheticInput.AltTester.WorldAutomationProbe";

    // The probe abandons a hold this long after its own duration; the poll ceiling sits just above.
    private const double COMPLETION_GRACE_SECONDS = 5;
    private const double POLL_MARGIN_SECONDS = 5;
    private const double POINTER_TIMEOUT_SECONDS = 3;

    public enum Gait
    {
        Walk,
        Jog,
        Run,
    }

    public static bool IsReady() =>
        CommonStuff.AltDriver.CallStaticMethod<bool>(PROBE_TYPE, "IsReady", ProbeOperation.PROBE_ASSEMBLY, new object[] { });

    /// <summary>The player's pose right now — read it around a gesture to assert what the gesture did.</summary>
    public static PlayerState PlayerState()
    {
        var result = ProbeOperation.Call(PROBE_TYPE, "GetPlayerStateJson", new object[] { }, "read player state").RequireOk();

        return new PlayerState(
            result.Position("position"),
            result.Position("rotationEuler"),
            result.Parcel("parcel"),
            result.Position("velocity"),
            result.Bool("isGrounded"));
    }

    /// <summary>
    /// Holds a camera-relative movement for <paramref name="seconds"/>: <paramref name="directionY"/> is
    /// forward (+1) / backward (-1), <paramref name="directionX"/> strafes right (+1) / left (-1). Scene
    /// movement locks apply exactly as they do to WASD unless <paramref name="ignoreInputModifiers"/>.
    /// </summary>
    public static WalkResult Walk(float directionX, float directionY, double seconds, Gait gait = Gait.Jog,
        bool jump = false, bool ignoreInputModifiers = false)
    {
        var description = $"{gait.ToString().ToLowerInvariant()} ({directionX},{directionY}) for {seconds}s";
        Reporter.Log($"Synthetic input: {description}");

        var result = ProbeOperation.Run(PROBE_TYPE, "StartWalk",
            new object[] { directionX, directionY, gait.ToString().ToLowerInvariant(), (float)seconds, jump, ignoreInputModifiers },
            seconds + COMPLETION_GRACE_SECONDS + POLL_MARGIN_SECONDS, description).RequireOk();

        var walk = new WalkResult(
            result.String("delivery"),
            result.Position("startPosition"),
            result.Position("endPosition"),
            result.Number("distance"),
            result.Parcel("parcel"));

        Reporter.Log($"Walk {walk.Delivery}: {walk.Start} -> {walk.End}, {walk.Distance:F2}m, parcel {walk.Parcel}");
        return walk;
    }

    /// <summary>Holds a relative camera look (mouse-delta units per frame) for <paramref name="seconds"/>.</summary>
    public static void CameraLook(float deltaX, float deltaY, double seconds)
    {
        Reporter.Log($"Synthetic input: camera look ({deltaX},{deltaY}) for {seconds}s");

        ProbeOperation.Run(PROBE_TYPE, "StartCameraLook", new object[] { deltaX, deltaY, (float)seconds },
            seconds + COMPLETION_GRACE_SECONDS + POLL_MARGIN_SECONDS, "camera look").RequireOk();
    }

    /// <summary>Turns the camera to aim at a world point.</summary>
    public static void LookAt(WorldPosition target)
    {
        Reporter.Log($"Synthetic input: look at {target}");

        ProbeOperation.Run(PROBE_TYPE, "StartLookAt", new object[] { (float)target.X, (float)target.Y, (float)target.Z },
            COMPLETION_GRACE_SECONDS + POLL_MARGIN_SECONDS, "look at").RequireOk();
    }

    /// <summary>
    /// Presses and releases a pointer button on a scene entity through the real reticle pipeline;
    /// <paramref name="button"/> is pointer, primary or secondary. Returns the pointer result (aim, hit,
    /// what the scene observed) without asserting it, since a refused aim is a legitimate outcome to test.
    /// </summary>
    public static ProbeResult ClickEntity(int entityId, string button = "pointer", string sceneId = "")
    {
        Reporter.Log($"Synthetic input: click entity {entityId} with {button}");

        return ProbeOperation.Run(PROBE_TYPE, "StartClickEntity", new object[] { entityId, sceneId, button, (float)POINTER_TIMEOUT_SECONDS },
            POINTER_TIMEOUT_SECONDS + POLL_MARGIN_SECONDS, $"click entity {entityId}");
    }

    /// <summary>
    /// Presses and releases an SDK input action with no aim (pointer, primary, secondary, jump, forward,
    /// action3..6, ...), so the scene root observes it — the same event a bare key press produces.
    /// </summary>
    public static ProbeResult PressInput(string action, double holdSeconds = 0)
    {
        Reporter.Log($"Synthetic input: press {action}" + (holdSeconds > 0 ? $" for {holdSeconds}s" : ""));

        return ProbeOperation.Run(PROBE_TYPE, "StartGlobalInput", new object[] { action, (float)holdSeconds },
            holdSeconds + POINTER_TIMEOUT_SECONDS + POLL_MARGIN_SECONDS, $"press {action}");
    }
}

public sealed record PlayerState(WorldPosition Position, WorldPosition RotationEuler, Parcel Parcel, WorldPosition Velocity, bool IsGrounded)
{
    public override string ToString() => $"position {Position}, yaw {RotationEuler.Y:F0}°, parcel {Parcel}, grounded {IsGrounded}";
}

/// <summary>What a movement hold did; <c>Delivery</c> is Completed, Preempted or TimedOut.</summary>
public sealed record WalkResult(string Delivery, WorldPosition Start, WorldPosition End, double Distance, Parcel Parcel)
{
    public bool Completed => Delivery == "Completed";
}
