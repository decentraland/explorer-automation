namespace ExplorerAutomation.Tests.Views.Elements;

/// <summary>
/// A locatable element whose text content can be read.
/// Use for dynamically populated labels, counters, titles, and other text elements
/// where you need to retrieve the displayed value but don't need to click or type.
/// </summary>
public record Readable(By by, string name) : Locatable(by, name)
{
    /// <summary>
    /// Waits for the element to appear and returns its current text content.
    /// </summary>
    /// <param name="timeout">Maximum seconds to wait for the element to appear.</param>
    /// <returns>The text displayed by the element.</returns>
    [AllureStep("Get text from object")]
    public string GetText(double timeout = 20D)
    {
        var altObject = WaitFor(timeout);
        return altObject.GetText();
    }
}
