namespace ExplorerAutomation.Tests.Views.Elements;

/// <summary>
/// Base element primitive that can be found, waited for, and checked for presence in the scene.
/// Use for non-interactive elements where you only need to verify existence or wait for appearance/disappearance.
/// </summary>
public record Locatable(By by, string name)
{
    [AllureStep("Wait for object to appear")]
    public AltObject WaitFor(double timeout = 20D)
    {
        Reporter.Log($"Waiting for object {this} to appear.");
        try
        {
            return CommonStuff.AltDriver.WaitForObject(by, name, timeout: timeout);
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
        try
        {
            CommonStuff.AltDriver.FindObject(by, name);
            return true;
        }
        catch (NotFoundException)
        {
            return false;
        }
    }
}