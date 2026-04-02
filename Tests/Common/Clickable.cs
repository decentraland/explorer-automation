namespace ExplorerAutomation.Tests.Common;

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