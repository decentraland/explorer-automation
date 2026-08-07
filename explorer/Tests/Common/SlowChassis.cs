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
    /// Kept well under the 300s AltDriver command-response ceiling set in GlobalSetup,
    /// and short enough that a genuinely broken interaction still fails inside a minute.
    /// </summary>
    public const double SETTLE_TIMEOUT = 60D;
}
