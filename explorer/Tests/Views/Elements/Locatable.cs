namespace ExplorerAutomation.Tests.Views.Elements;

/// <summary>
/// Base element primitive that can be found, waited for, and checked for presence in the scene.
/// Use for non-interactive elements where you only need to verify existence or wait for appearance/disappearance.
/// Every verification method (WaitFor/WaitForGone/IsPresent) attaches a verification screenshot
/// at the moment the check completes via <see cref="Reporter.TakeVerificationShot"/>.
/// </summary>
public record Locatable(By by, string name)
{
    /// <summary>
    /// Short human-readable label for verification-shot names: the leaf segment of the locator
    /// (path locators like "//Canvas/Foo/Bar" become "Bar"; plain names pass through).
    /// </summary>
    internal string ShotName
    {
        get
        {
            var idx = name.LastIndexOf('/');
            return idx >= 0 && idx < name.Length - 1 ? name[(idx + 1)..] : name;
        }
    }

    [AllureStep("Wait for object to appear")]
    public AltObject WaitFor(double timeout = 20D) => WaitFor(timeout, verificationShot: true);

    // Shot-suppressed overload for action helpers (Click/SetText) and verifications that take
    // their own shot afterwards (GetText) — actions must not capture, and verifications must
    // not capture twice.
    internal AltObject WaitFor(double timeout, bool verificationShot)
    {
        Reporter.Log($"Waiting for object {this} to appear.");
        try
        {
            var altObject = CommonStuff.AltDriver.WaitForObject(by, name, timeout: timeout);
            if (verificationShot)
                Reporter.TakeVerificationShot($"appeared_{ShotName}");
            return altObject;
        }
        catch (WaitTimeOutException)
        {
            Reporter.Log($"Object {this} was not found within {timeout} seconds");
            throw new AssertionException(
                $"Object '{this}' was not found within {timeout} seconds. Please check if the object exists or if the game loaded correctly.");
        }
    }

    [AllureStep("Wait for object to disappear")]
    public void WaitForGone(double timeout = 20D)
    {
        Reporter.Log($"Waiting for object {this} to disappear.");
        try
        {
            CommonStuff.AltDriver.WaitForObjectNotBePresent(by, name, timeout: timeout);
            Reporter.TakeVerificationShot($"gone_{ShotName}");
        }
        catch (WaitTimeOutException)
        {
            Reporter.Log($"Object {this} did not disappear within {timeout} seconds");
            throw new AssertionException(
                $"Object '{this}' did not disappear within within {timeout} seconds.");
        }
    }

    [AllureStep("Check if object present")]
    public bool IsPresent()
    {
        bool present;
        try
        {
            CommonStuff.AltDriver.FindObject(by, name);
            present = true;
        }
        catch (NotFoundException)
        {
            present = false;
        }

        // Presence checks back asserts in either direction, so capture the frame for both
        // outcomes — the label records which state was observed.
        Reporter.TakeVerificationShot($"{(present ? "present" : "absent")}_{ShotName}");
        return present;
    }
}
