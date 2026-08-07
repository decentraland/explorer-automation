namespace ExplorerAutomation.Tests.Views.Elements;

/// <summary>
/// A locatable element that can be clicked.
/// Use for buttons, toggles, checkboxes, tabs, and any interactive element that responds to clicks.
/// </summary>
public record Clickable(By by, string name) : Locatable(by, name)
{
    [AllureStep("Click on object")]
    public void Click()
    {
        // Shot-suppressed wait: Click is an action, not a verification, so no screenshot here.
        var altObject = WaitFor(20D, verificationShot: false);
        altObject.Click();
        Thread.Sleep(200);
    }

    [AllureStep("Tap on object")]
    public void Tap()
    {
        // Some buttons in this build ignore the synthetic Click event but respond to Tap
        // (pointer down/up) — e.g. the Places category chips and the backpack hover
        // overlays. Prefer Click; reach for Tap when a verified click has no effect.
        var altObject = WaitFor(20D, verificationShot: false);
        altObject.Tap();
        Thread.Sleep(200);
    }

    /// <summary>
    /// Clicks, then falls back to <see cref="Tap"/> when <paramref name="responded"/> has not
    /// become true within <paramref name="graceSeconds"/>. Encodes the "prefer Click, reach for
    /// Tap" rule above as control flow, for the controls where which one works is chassis-
    /// dependent rather than fixed: Click moves the pointer before pressing, so on a slow
    /// chassis a hover overlay can render into the gap and swallow the press, while Tap presses
    /// the object directly. Callers should still follow with an authoritative WaitFor so a
    /// genuine failure produces the standard error.
    /// </summary>
    [AllureStep("Click on object, falling back to tap")]
    public void ClickOrTap(Func<bool> responded, double graceSeconds = 5)
    {
        Click();
        for (var elapsed = 0.0; elapsed < graceSeconds; elapsed += 0.5)
        {
            if (responded())
                return;
            Thread.Sleep(500);
        }

        Reporter.Log($"Click on {this} produced no response within {graceSeconds}s — falling back to Tap");
        Tap();
    }
}