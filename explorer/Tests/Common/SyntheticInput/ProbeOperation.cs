using System.Text.Json;

namespace ExplorerAutomation.Tests.Common.SyntheticInput;

/// <summary>
/// Start/poll bridge for the client's synthetic-input and navigation probes. A multi-frame gesture
/// cannot complete inside one <c>CallStaticMethod</c>, so the probe returns an operation id and the
/// test polls <c>PollJson</c> until the payload is ready.
/// </summary>
public static class ProbeOperation
{
    public const string PROBE_ASSEMBLY = "DCL.SyntheticInput";

    private const int POLL_INTERVAL_MS = 250;

    /// <summary>
    /// Starts <paramref name="startMethod"/> on <paramref name="probeType"/> and polls it to completion.
    /// <paramref name="timeoutSeconds"/> is the test-side ceiling; the probe applies its own inside the
    /// payload, so size this a little above the gesture's own duration.
    /// </summary>
    public static ProbeResult Run(string probeType, string startMethod, object[] args, double timeoutSeconds, string description)
    {
        var operationId = CommonStuff.AltDriver.CallStaticMethod<int>(probeType, startMethod, PROBE_ASSEMBLY, args);
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);

        while (true)
        {
            var json = CommonStuff.AltDriver.CallStaticMethod<string>(probeType, "PollJson", PROBE_ASSEMBLY, new object[] { operationId });
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.GetProperty("done").GetBoolean())
            {
                if (root.TryGetProperty("result", out var result))
                    return new ProbeResult(result.Clone(), description);

                // The registry itself answered — an evicted or unknown operation id.
                throw new AssertionException($"{description}: {root.GetProperty("error").GetString()}");
            }

            if (DateTime.UtcNow > deadline)
                throw new AssertionException($"{description} did not finish within {timeoutSeconds}s (operation {operationId})");

            Thread.Sleep(POLL_INTERVAL_MS);
        }
    }

    /// <summary>One-round-trip probe call whose payload is already the result.</summary>
    public static ProbeResult Call(string probeType, string method, object[] args, string description)
    {
        var json = CommonStuff.AltDriver.CallStaticMethod<string>(probeType, method, PROBE_ASSEMBLY, args);
        using var doc = JsonDocument.Parse(json);
        return new ProbeResult(doc.RootElement.Clone(), description);
    }
}

/// <summary>A probe payload: every one carries <c>ok</c>, and an <c>error</c> when it is false.</summary>
public sealed class ProbeResult(JsonElement root, string description)
{
    public JsonElement Root => root;

    public bool Ok => root.TryGetProperty("ok", out var ok) && ok.GetBoolean();

    public string Error => root.TryGetProperty("error", out var error) ? error.GetString() : null;

    /// <summary>Fails the test with the probe's own error when the payload reports a failure.</summary>
    public ProbeResult RequireOk()
    {
        if (!Ok)
            throw new AssertionException($"{description} failed: {Error ?? root.ToString()}");

        return this;
    }

    public string String(string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    public bool Bool(string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;

    public double Number(string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetDouble() : double.NaN;

    public WorldPosition Position(string name) => WorldPosition.From(root.GetProperty(name));

    public Parcel Parcel(string name) => SyntheticInput.Parcel.From(root.GetProperty(name));

    public override string ToString() => root.ToString();
}

/// <summary>World-space meters; one parcel is 16x16 on X/Z.</summary>
public readonly record struct WorldPosition(double X, double Y, double Z)
{
    public static WorldPosition From(JsonElement element) =>
        new(element.GetProperty("x").GetDouble(), element.GetProperty("y").GetDouble(), element.GetProperty("z").GetDouble());

    public double DistanceTo(WorldPosition other)
    {
        var dx = X - other.X;
        var dy = Y - other.Y;
        var dz = Z - other.Z;
        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    /// <summary>Distance ignoring height — what a walk on flat ground should have covered.</summary>
    public double FlatDistanceTo(WorldPosition other)
    {
        var dx = X - other.X;
        var dz = Z - other.Z;
        return Math.Sqrt(dx * dx + dz * dz);
    }

    public override string ToString() => $"({X:F2}, {Y:F2}, {Z:F2})";
}

public readonly record struct Parcel(int X, int Y)
{
    public static Parcel From(JsonElement element) =>
        new(element.GetProperty("x").GetInt32(), element.GetProperty("y").GetInt32());

    public override string ToString() => $"{X},{Y}";
}
