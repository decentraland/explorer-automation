using System.Diagnostics;

namespace ExplorerAutomation.Tests.Views.ExplorePanelSections;

/// <summary>
/// Abstract base class for sections within a panel (e.g. explore panel tabs).
/// Wraps a section-level root locator so each section can independently check its own visibility.
/// </summary>
public abstract class BaseSection(Locatable sectionLocator) : BaseView(sectionLocator)
{
    private static readonly Locatable PanelRoot = new(By.NAME, "ExplorePanelUI(Clone)");

    // Gap between panel-raycaster reads while settling.
    private const int RAYCASTER_POLL_MS = 250;

    // Floor for the find's share of the budget once the signal wait has spent part of it.
    private const double MIN_FIND_TIMEOUT = 1D;

    // Sections are not MVC views; the panel that hosts them is, so a section's own readiness
    // waits on the panel reporting Shown.
    internal override AltObject WaitFor(double timeout, bool verificationShot)
    {
        var elapsed = Stopwatch.StartNew();
        ViewSignal.WaitForShown("ExplorePanelView", timeout);

        var remaining = Math.Max(timeout - elapsed.Elapsed.TotalSeconds, MIN_FIND_TIMEOUT);
        return base.WaitFor(remaining, verificationShot);
    }

    /// <summary>
    /// Blocks until the panel's GraphicRaycaster reads enabled on
    /// <see cref="SlowChassis.SETTLE_READS"/> consecutive samples, so a momentary flicker
    /// while remote content loads isn't mistaken for the panel actually being interactive.
    /// </summary>
    protected static void WaitForPanelInteractive()
    {
        var panel = PanelRoot.WaitFor(10D, verificationShot: false);
        var deadline = Stopwatch.StartNew();
        var consecutive = 0;

        while (deadline.Elapsed.TotalSeconds < SlowChassis.SETTLE_TIMEOUT)
        {
            var enabled = panel.GetComponentProperty<bool>(
                "UnityEngine.UI.GraphicRaycaster", "enabled", "UnityEngine.UI");
            consecutive = enabled ? consecutive + 1 : 0;
            if (consecutive >= SlowChassis.SETTLE_READS)
                return;

            Thread.Sleep(RAYCASTER_POLL_MS);
        }

        throw new AssertionException(
            $"Panel GraphicRaycaster never read enabled on {SlowChassis.SETTLE_READS} "
            + $"consecutive reads within {SlowChassis.SETTLE_TIMEOUT}s.");
    }
}