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
}