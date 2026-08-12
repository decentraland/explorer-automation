namespace ExplorerAutomation.Tests.Views;

/// <summary>
/// Opens one of the passport's edit affordances (the header name pencil, the About Me pencil),
/// clearing the SoftMask raycast veto first and reporting what the press did.
/// </summary>
internal static class PassportEditPress
{
    private static readonly Locatable PASSPORT_ROOT = new(By.NAME, "Passport(Clone)");

    private const double AFFORDANCE_TIMEOUT = 5D;   // the pencil is either there or it is not
    private const int    POINTER_SETTLE_MS  = 500;
    private const int    POLL_MS            = 250;
    private const double TOAST_TIMEOUT      = 10D;  // the toast hides itself after five
    private const string TOAST_COMPONENT       = "DCL.UI.WarningNotificationView";
    private const string TOAST_ASSEMBLY        = "UI";
    private const string CANVAS_GROUP_ASSEMBLY = "UnityEngine.CoreModule";

    /// <summary>
    /// Presses <paramref name="affordance"/> once and waits for <paramref name="opened"/>, failing
    /// with whether the passport survived the press.
    /// </summary>
    [AllureStep("Press a passport edit affordance")]
    internal static void Open(Clickable affordance, Func<bool> opened, string what)
    {
        // Park the cursor before pressing: Click queues the pointer move and the press together,
        // so the press can land on the frame the arriving pointer is re-running hover on.
        var altObject = affordance.WaitFor(AFFORDANCE_TIMEOUT, verificationShot: false);
        CommonStuff.AltDriver.MoveMouse(new AltVector2(altObject.x, altObject.y));
        Thread.Sleep(POINTER_SETTLE_MS);

        Reporter.TakeVerificationShot($"beforepress_{what}");
        ClearErrorNotification();
        DisableSoftMasks("//Passport(Clone)/BackgroundContainer",
            "//Passport(Clone)/BackgroundContainer/Scroll View/Viewport");

        PASSPORT_ROOT.WaitFor(AFFORDANCE_TIMEOUT, verificationShot: false)
            .WaitForComponentProperty("UnityEngine.UI.GraphicRaycaster", "enabled", true,
                "UnityEngine.UI", timeout: SlowChassis.SETTLE_TIMEOUT);

        // One press only. A second one is destructive here: the passport's backdrop is a
        // full-screen close Button, so any press it swallows takes the panel with it. No settle —
        // the poll below waits on the state the press produces.
        affordance.Click(settleMs: 0);

        // Polling continues past a passport close because the name editor is a separate top-level
        // modal and can still arrive — collapsing the two makes the failure unreadable.
        var started = DateTime.UtcNow;
        var deadline = started.AddSeconds(SlowChassis.SETTLE_TIMEOUT);
        double? closedAfter = null;

        while (DateTime.UtcNow < deadline)
        {
            if (opened())
            {
                Reporter.TakeVerificationShot($"opened_{what}");
                Reporter.Log($"{what} opened {(DateTime.UtcNow - started).TotalSeconds:F1}s after the press");
                return;
            }

            if (closedAfter == null && !PASSPORT_ROOT.IsPresent(verificationShot: false))
            {
                closedAfter = (DateTime.UtcNow - started).TotalSeconds;
                Reporter.TakeVerificationShot($"passportclosed_{what}");
            }

            Thread.Sleep(POLL_MS);
        }

        Reporter.TakeVerificationShot($"timeout_{what}");
        throw new AssertionException(
            $"{what} did not open within {SlowChassis.SETTLE_TIMEOUT}s of pressing {affordance} at "
            + $"({altObject.x},{altObject.y}). "
            + (closedAfter == null
                ? "The passport stayed open, so something absorbed the press that is not a close "
                  + "affordance."
                : $"The passport closed {closedAfter:F1}s in, so the press reached its backdrop "
                  + "rather than the button — check whether a SoftMask is vetoing the raycast."));
    }

    /// <summary>
    /// Stops the passport's error toast intercepting a press.
    /// </summary>
    /// <remarks>
    /// Thumbnail loads fail on this chassis, so the passport raises its <c>WarningNotificationView</c>
    /// on open. It renders over the header, covering the name pencil, and blocks raycasts for the
    /// five seconds it is up — a press it swallows does nothing, since it is not a close affordance.
    /// Whether it is still up when the test presses is a race the test would otherwise lose at
    /// random, so the toast is dismissed through its own <c>Hide</c> rather than by reaching into
    /// its CanvasGroup — that would reimplement the client and rot the moment <c>Hide</c> changes.
    /// Falls back to waiting the toast out if the call cannot be made.
    /// </remarks>
    private static void ClearErrorNotification()
    {
        var toast = new Locatable(By.PATH, "//Passport(Clone)/ErrorNotification");
        if (!toast.IsPresent(verificationShot: false))
            return;

        try
        {
            // Both arguments are passed even though both are optional: the SDK selects an overload
            // by parameter count and does not fill defaults in. An empty type list makes it match
            // on count alone, and an empty JSON object deserializes to a default CancellationToken.
            toast.WaitFor(AFFORDANCE_TIMEOUT, verificationShot: false)
                .CallComponentMethod<object>(TOAST_COMPONENT, "Hide", TOAST_ASSEMBLY,
                    new object[] { true, new object() }, new string[0]);
            Reporter.Log("Passport error toast dismissed via Hide(instant: true)");
            return;
        }
        catch (Exception ex)
        {
            Reporter.Log($"Could not dismiss the passport error toast ({ex.Message}) — waiting it out");
        }

        var deadline = DateTime.UtcNow.AddSeconds(TOAST_TIMEOUT);
        while (DateTime.UtcNow < deadline && IsToastBlocking(toast))
            Thread.Sleep(POLL_MS);
    }

    private static bool IsToastBlocking(Locatable toast)
    {
        try
        {
            return toast.WaitFor(AFFORDANCE_TIMEOUT, verificationShot: false)
                .GetComponentProperty<bool>("UnityEngine.CanvasGroup", "blocksRaycasts",
                    CANVAS_GROUP_ASSEMBLY);
        }
        catch (Exception)
        {
            return false;   // unreadable is not a reason to burn the ceiling
        }
    }

    /// <summary>
    /// Switches off the <c>Coffee.UISoftMask.SoftMask</c> components covering a press target.
    /// </summary>
    /// <remarks>
    /// SoftMask is an <c>ICanvasRaycastFilter</c> and Unity consults the filters on a hit graphic's
    /// ancestors, so one on a container vetoes every press inside its subtree. In the passport those
    /// presses fall through to <c>Background_Close</c>, a full-screen close Button, so the panel
    /// closes instead of responding. Disabling the component makes its filter permissive.
    /// Best-effort per path: a missing container is not worth failing a test over.
    /// </remarks>
    internal static void DisableSoftMasks(params string[] paths)
    {
        foreach (var path in paths)
        {
            try
            {
                new Locatable(By.PATH, path)
                    .WaitFor(AFFORDANCE_TIMEOUT, verificationShot: false)
                    .SetComponentProperty("Coffee.UISoftMask.SoftMask", "enabled", false,
                        "Coffee.SoftMaskForUGUI");
            }
            catch (Exception ex)
            {
                Reporter.Log($"SoftMask on {path}: could not disable — {ex.Message}");
            }
        }
    }
}
