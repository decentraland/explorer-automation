namespace ExplorerAutomation.Tests.Common;

/// <summary>
/// Dismisses the client's performance-hiccup modal (<c>DCL.BugReporting.UI.PerformanceIssuePromptView</c>)
/// if it is up. <c>PerformanceIssuePromptSystem</c> shows this full-screen, own-raycaster popup whenever
/// it detects a frame-time hiccup — which the underpowered CI hardware can trigger at any point in a
/// run, not just at boot — and it swallows every click behind it until closed. Call before any
/// click/keypress whose target could be covered by it, not just once at fixture start.
/// </summary>
public static class PerformanceIssuePrompt
{
    private static readonly Locatable Root               = new(By.NAME, "PerformanceIssuePrompt");
    private static readonly Clickable DontShowAgainToggle = new(By.PATH, "//PerformanceIssuePrompt/Panel/DontShowAgainToggle");
    private static readonly Clickable CloseButton         = new(By.PATH, "//PerformanceIssuePrompt/Panel/ButtonsRow/CloseButton");

    /// <summary>
    /// Ticks "Don't show this again" before closing. The client re-offers the prompt on every
    /// detected hiccup until that opt-out is set, and CI's paravirt hardware hiccups often enough
    /// that a plain Close would just have it back a few seconds later.
    /// </summary>
    public static void DismissIfPresent()
    {
        if (!Root.IsPresent(verificationShot: false))
            return;

        Reporter.Log("Performance issue prompt is up — opting out and closing it so it stops swallowing clicks");

        // Best-effort: every caller of this is itself inside a click/retry loop that this modal
        // was blocking, and the caller's own click + responded-check afterward is the
        // authoritative signal. If the toggle/close click misses or the fade-out runs past the
        // 5s WaitForGone — exactly the kind of slowness this suite's underpowered CI hardware
        // produces — swallow it here rather than let it escape as an unrelated-looking
        // AssertionException and fail the caller before its own retry logic ever runs. If the
        // prompt is genuinely still up afterward, the caller's next click attempt hits it again
        // and gets another chance to dismiss it.
        try
        {
            DontShowAgainToggle.Click(settleMs: 0);
            CloseButton.Click(settleMs: 0);
            Root.WaitForGone(5, verificationShot: false);
        }
        catch (AssertionException ex)
        {
            Reporter.Log($"Could not fully dismiss the performance issue prompt, continuing anyway: {ex.Message}");
        }
    }
}
