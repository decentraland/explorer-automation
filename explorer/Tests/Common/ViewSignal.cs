using System.Diagnostics;

namespace ExplorerAutomation.Tests.Common;

/// <summary>
/// Reads MVC view lifecycle state out of the running Explorer through
/// <c>MVC.AltTesterViewProbe</c>. A view's GameObject is active for the whole show and hide
/// animation, so object presence cannot answer "is this view ready".
/// </summary>
public static class ViewSignal
{
    private const string PROBE_TYPE = "MVC.AltTesterViewProbe";
    private const string PROBE_ASSEMBLY = "MVC";

    public const string SHOWN = "Shown";
    public const string HIDDEN = "Hidden";
    public const string UNKNOWN = "Unknown";

    // One round trip per poll, ~165ms; 100ms of sleep on top keeps a tight loop off the wire
    // without adding a meaningful floor to the wait.
    private const int POLL_MS = 100;

    private static bool? _available;

    /// <summary>Whether the running build ships the probe at all.</summary>
    public static bool IsAvailable
    {
        get
        {
            if (_available.HasValue) return _available.Value;

            try
            {
                KnownViews();
                _available = true;
            }
            catch (Exception ex)
            {
                _available = false;
                Reporter.Log($"View signal: this build does not carry {PROBE_TYPE} — {ex.Message}");
            }

            return _available.Value;
        }
    }

    public static string GetState(string viewName) =>
        CommonStuff.AltDriver.CallStaticMethod<string>(
            PROBE_TYPE, "GetState", PROBE_ASSEMBLY, new object[] { viewName });

    public static bool IsShown(string viewName) => GetState(viewName) == SHOWN;

    [AllureStep("Wait for view to be shown")]
    public static void WaitForShown(string viewName, double timeout = 20D) =>
        WaitForState(viewName, timeout, state => state == SHOWN, "shown");

    // A view the client has never reported cannot be asserted hidden: that state is
    // indistinguishable from a misspelled name, and every view wait is built on this.
    [AllureStep("Wait for view to be hidden")]
    public static void WaitForHidden(string viewName, double timeout = 20D)
    {
        if (GetState(viewName) == UNKNOWN)
            throw new AssertionException(
                $"View '{viewName}' has never reported to the client this session, so it cannot "
                + $"be asserted hidden. Check the name against: {KnownViews()}");

        WaitForState(viewName, timeout, state => state == HIDDEN, "hidden");
    }

    private static void WaitForState(string viewName, double timeout, Func<string, bool> satisfied, string what)
    {
        var deadline = Stopwatch.StartNew();
        var last = UNKNOWN;

        while (deadline.Elapsed.TotalSeconds < timeout)
        {
            last = GetState(viewName);
            if (satisfied(last)) return;
            Thread.Sleep(POLL_MS);
        }

        throw new AssertionException(
            $"View '{viewName}' was not {what} within {timeout} seconds (last state: {last}). "
            + $"Every view and its state: {Snapshot()}");
    }

    private static string KnownViews() =>
        CommonStuff.AltDriver.CallStaticMethod<string>(
            PROBE_TYPE, "GetKnownViews", PROBE_ASSEMBLY, new object[] { });

    /// <summary>Every view and its state, for attaching to a failure report.</summary>
    public static string Snapshot() =>
        CommonStuff.AltDriver.CallStaticMethod<string>(
            PROBE_TYPE, "Snapshot", PROBE_ASSEMBLY, new object[] { });
}
