namespace ExplorerAutomation.Tests.Common;

public record Writable(By by, string name) : Clickable(by, name)
{
    [AllureStep("Set text on object")]
    public void SetText(string text, float timeout = 10.0f)
    {
        var altObject = WaitFor(timeout);
        altObject.SetText(text);
    }

    [AllureStep("Get text from object")]
    public string GetText(float timeout = 10.0f)
    {
        var altObject = WaitFor(timeout);
        return altObject.GetText();
    }
}