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

    /// <summary>
    /// Polls the element's text until it satisfies <paramref name="predicate"/> or the timeout
    /// elapses, returning whatever text was last read. Use for labels/counters that populate
    /// or update asynchronously after an action (search results, tab counters, info panels)
    /// instead of guessing a fixed settle time.
    /// </summary>
    public string WaitForText(Func<string, bool> predicate, double timeoutSeconds = 10, double pollIntervalSeconds = 0.5) =>
        WaitForText(predicate, timeoutSeconds, pollIntervalSeconds, verificationShot: true);

    // Shot-suppressed overload for settle loops that call this several times for one logical
    // read (re-reading a streaming grid's label until two reads agree): only the read the
    // caller keeps is a verification, so the caller takes the single shot.
    [AllureStep("Wait for text to match")]
    internal string WaitForText(Func<string, bool> predicate, double timeoutSeconds, double pollIntervalSeconds, bool verificationShot)
    {
        // Shot-suppressed reads inside the poll loop (see PassportView.WaitForUserName): a
        // capture per iteration would both spam the report and eat the wall-clock deadline.
        var text = string.Empty;
        var matched = false;
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            // Bound the element wait by what is left of the caller's budget. Hardcoding it
            // meant a caller asking for 40s failed at 20s, and one asking for 10s blocked for
            // 20s on a single read.
            text = GetText(Math.Max((deadline - DateTime.UtcNow).TotalSeconds, 1D), verificationShot: false);
            if (predicate(text))
            {
                matched = true;
                break;
            }
            Thread.Sleep(TimeSpan.FromSeconds(pollIntervalSeconds));
        }

        if (verificationShot)
            Reporter.TakeVerificationShot($"{(matched ? "text" : "timeout")}_{ShotName}");
        return text;
    }
}
