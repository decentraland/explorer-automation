namespace ExplorerAutomation.Tests.Views.Elements;

/// <summary>
/// A clickable element that also supports text input and output.
/// Use for input fields, search bars, and other text entry elements where you need to type and read values.
/// </summary>
public record Writable(By by, string name) : Clickable(by, name)
{
    [AllureStep("Set text on object")]
    public void SetText(string text, bool submit = true, float timeout = 10.0f)
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