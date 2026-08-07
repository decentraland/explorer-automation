namespace ExplorerAutomation.Tests.Common;

/// <summary>
/// Wait ceilings sized for the slowest supported chassis — the GH-hosted macos-14
/// runner, an Apple Paravirtual VM with 3 vCPU and no GPU.
/// </summary>
public static class SlowChassis
{
    /// <summary>
    /// Ceiling for waits on the client reflecting a state mutation the test just made:
    /// an equipped-slot badge lighting up, an inline editor swapping in, a detail panel
    /// instantiating, a Toggle's isOn settling. The stock 20s element wait and the
    /// shorter per-call timeouts it wraps (10s SetText, 5s component-property) are all
    /// observed to expire on paravirt while the mutation is still in flight, so these
    /// waits take this ceiling instead of a per-call-site constant.
    /// Kept well under the 300s AltDriver command-response ceiling set in GlobalSetup, and
    /// deliberately not larger: the workflow caps the suite step at 40 minutes, and every
    /// second spent here is only ever spent on a path that is already failing. 60s measurably
    /// pushed a failing run to ~35 minutes (CI run 31176916555).
    /// </summary>
    public const double SETTLE_TIMEOUT = 40D;

    /// <summary>
    /// Re-reads allowed while waiting for a streaming grid's leading item to stop changing.
    /// A value is trusted once two consecutive reads agree; this bounds how long the grid is
    /// given to settle before a still-moving value is accepted anyway.
    /// </summary>
    public const int SETTLE_READS = 3;
}
