using System.Text.Json;

namespace ExplorerAutomation.Tests.Common;

/// <summary>
/// Reads feature flag state out of the running Explorer through
/// <c>DCL.FeatureFlags.AlttesterFeatureFlagsProbe</c>, so a test covering flag-gated UI asserts
/// what the client resolved rather than a hard-coded expectation that a flag flip turns into a
/// permanent failure.
///
/// Fetching the flags document here instead is not equivalent, in both directions: Unleash
/// evaluates its hostname strategy from the request's <c>referer</c> header, so a bare fetch
/// answers with a subset in which live features read as off, and the client then folds app
/// arguments and editor overrides on top of whatever the document says.
/// </summary>
public static class FeatureFlags
{
    private const string PROBE_TYPE = "DCL.FeatureFlags.AlttesterFeatureFlagsProbe";
    private const string PROBE_ASSEMBLY = "DCL.Network";

    /// <summary>What the client's gate says about a piece of UI.</summary>
    public enum Expected
    {
        Present,
        Absent,

        /// <summary>The client decides per-user by something this side can't see — don't assert.</summary>
        Unknown,
    }

    // The client fetches its document once during login and holds it for the process, so a value
    // cannot change mid-run and one round trip per key is enough.
    private static readonly Dictionary<string, bool> _features = new();
    private static readonly Dictionary<string, bool> _flags = new();
    private static bool _loggedStatus;

    /// <summary>Resolved <c>FeatureId</c> state — the flag with app arguments and editor overrides folded in.</summary>
    public static bool IsFeatureEnabled(string featureId)
    {
        LogStatusOnce();
        if (_features.TryGetValue(featureId, out bool cached)) return cached;

        bool enabled = CommonStuff.AltDriver.CallStaticMethod<bool>(
            PROBE_TYPE, "IsFeatureEnabled", PROBE_ASSEMBLY, new object[] { featureId });
        _features[featureId] = enabled;
        return enabled;
    }

    /// <summary>Raw remote flag state, keyed without the <c>explorer-</c> prefix the server carries.</summary>
    public static bool IsFlagEnabled(string flagId)
    {
        LogStatusOnce();
        if (_flags.TryGetValue(flagId, out bool cached)) return cached;

        bool enabled = CommonStuff.AltDriver.CallStaticMethod<bool>(
            PROBE_TYPE, "IsFlagEnabled", PROBE_ASSEMBLY, new object[] { flagId });
        _flags[flagId] = enabled;
        return enabled;
    }

    /// <summary>
    /// Gate for UI the client shows on a resolved <c>FeatureId</c>. Definitive in both directions,
    /// so the test still catches the inverse bug — UI that lingers after its flag goes off.
    /// </summary>
    public static Expected Feature(string featureId) =>
        IsFeatureEnabled(featureId) ? Expected.Present : Expected.Absent;

    /// <summary>
    /// Gate for UI behind a flag plus that flag's <c>wallets</c> allowlist — Marketplace Credits and
    /// Communities both resolve this way. Off is definitive; on is only definitive while the
    /// allowlist is empty, because the run's wallet isn't known here.
    /// </summary>
    public static Expected UserGated(string flagId)
    {
        if (!IsFlagEnabled(flagId)) return Expected.Absent;
        return string.IsNullOrEmpty(WalletsAllowlist(flagId)) ? Expected.Present : Expected.Unknown;
    }

    public static string StatusJson() =>
        CommonStuff.AltDriver.CallStaticMethod<string>(
            PROBE_TYPE, "GetStatusJson", PROBE_ASSEMBLY, new object[] { });

    private static string WalletsAllowlist(string flagId)
    {
        var json = CommonStuff.AltDriver.CallStaticMethod<string>(
            PROBE_TYPE, "GetFlagVariantJson", PROBE_ASSEMBLY, new object[] { flagId });

        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("present", out var present) || !present.GetBoolean())
            return null;
        if (!doc.RootElement.TryGetProperty("name", out var name) || name.GetString() != "wallets")
            return null;

        return doc.RootElement.TryGetProperty("payloadValue", out var value) ? value.GetString() : null;
    }

    // One line per run recording what the client resolved, so a failure caused by a flag flip is
    // readable from the report without a rerun.
    private static void LogStatusOnce()
    {
        if (_loggedStatus) return;
        _loggedStatus = true;
        Reporter.Log($"Feature flags: {StatusJson()}");
    }
}
