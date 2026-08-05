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
    public string GetText(double timeout = 20D) => GetText(timeout, verificationShot: true);

    // Shot-suppressed overload for polling loops in views (e.g. waiting for a label to refresh):
    // per-poll reads must not capture — the caller takes one shot when its wait completes.
    [AllureStep("Get text from object")]
    internal string GetText(double timeout, bool verificationShot)
    {
        // Suppress the WaitFor shot — the verification moment is the text read, captured below.
        var altObject = WaitFor(timeout, verificationShot: false);
        var text = altObject.GetText();
        if (verificationShot)
            Reporter.TakeVerificationShot($"text_{ShotName}");
        return text;
    }
}
