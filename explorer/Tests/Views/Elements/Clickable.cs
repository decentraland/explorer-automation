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
}