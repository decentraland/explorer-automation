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
        var altObject = WaitFor();
        altObject.Click();
        Thread.Sleep(200);
    }
}