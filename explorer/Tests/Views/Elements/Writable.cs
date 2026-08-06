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
        // Shot-suppressed wait: SetText is an action, not a verification, so no screenshot here.
        var altObject = WaitFor(timeout, verificationShot: false);
        altObject.SetText(text, submit);
    }

    public string GetText(float timeout = 10.0f) => GetText(timeout, verificationShot: true);

    // Shot-suppressed overload for polling loops in views (e.g. waiting for an input pre-fill):
    // per-poll reads must not capture — the caller takes one shot when its wait completes.
    [AllureStep("Get text from object")]
    internal string GetText(float timeout, bool verificationShot)
    {
        // Suppress the WaitFor shot — the verification moment is the text read, captured below.
        var altObject = WaitFor(timeout, verificationShot: false);
        var text = altObject.GetText();
        if (verificationShot)
            Reporter.TakeVerificationShot($"text_{ShotName}");
        return text;
    }
}