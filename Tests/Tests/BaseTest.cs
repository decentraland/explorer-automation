namespace ExplorerAutomation.Tests.Tests;

[TestFixture]
[AllureNUnit]
public class BaseTest
{
    protected Exception ExceptionFromOneTimeSetUp;

    protected ViewContainer Views => ViewContainer.Instance;
    protected AltDriver AltDriver => CommonStuff.AltDriver;

    #region Setup and Teardown

    [OneTimeSetUp]
    [AllureBefore("Ensure the player is in world")]
    public void OneTimeSetUp()
    {
        EnsureInWorld();
    }

    [SetUp]
    [AllureBefore("Set up before each test")]
    public void SetUp()
    {
        Reporter.Log($"Starting test: {TestContext.CurrentContext.Test.Name}");

        // In case a popup is opened, this will close it
        PressEscape();
    }

    [TearDown]
    [AllureAfter("Clean up after each test")]
    public void TearDown()
    {
        var testResult = TestContext.CurrentContext.Result.Outcome.Status;
        Reporter.Log($"Test {TestContext.CurrentContext.Test.Name} completed with status: {testResult}");

        if (testResult == NUnit.Framework.Interfaces.TestStatus.Failed)
        {
            Reporter.Log("Test failed - taking screenshot for debugging");
            Reporter.TakeScreenshot("" + TestContext.CurrentContext.Test.Name + "_Failed");
        }
    }

    #endregion

    #region In-World Setup

    [AllureStep("Ensure player is in-world")]
    private void EnsureInWorld()
    {
        if (Views.SplashScreen.IsPresent() || Views.AuthenticationMainScreen.IsPresent())
        {
            Reporter.Log("Authentication / splash screen detected — entering world");
            Views.SplashScreen.WaitForGone();
            Views.AuthenticationMainScreen.WaitFor();
            Views.AuthenticationMainScreen.JumpIntoWorldButton.Click();
            Views.LoadingScreen.WaitFor(30);
            Views.LoadingScreen.WaitForGone(120);
        }
        else
        {
            Reporter.Log("Already in-world — skipping authentication");
        }

        Views.MainMenu.WaitFor(120);
        Reporter.Log("Player is in-world and main menu is ready");
    }

    #endregion


    [AllureStep("Wait for a specified duration")]
    public void Wait(double seconds)
    {
        Thread.Sleep(TimeSpan.FromSeconds(seconds));
    }

    #region Input Helpers

    [AllureStep("Press key")]
    protected void PressKey(AltKeyCode keyCode, float delay = 0.5f)
    {
        Reporter.Log($"Pressing key: {keyCode}");
        AltDriver.PressKey(keyCode);
        Thread.Sleep((int)(1000L * delay));
    }

    [AllureStep("Press Escape")]
    protected void PressEscape()
    {
        PressKey(AltKeyCode.Escape);
    }

    #endregion
}
