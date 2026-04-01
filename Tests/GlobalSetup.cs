namespace ExplorerAutomation.Tests;

[SetUpFixture]
public class GlobalSetup
{
    [OneTimeSetUp]
    public void RunBeforeAllTests()
    {
        StartDriver();
        ViewContainer.Initialize();
        Reporter.SetupUnityLogListener();
    }

    [OneTimeTearDown]
    public void RunAfterAllTests()
    {
        Reporter.AddUnityLogsToAllure();
        StopDriver();
    }

    [AllureBefore("Start AltTester Driver")]
    public void StartDriver()
    {
        Reporter.Log($"Connecting to AltTester at 127.0.0.1:13000");

        CommonStuff.AltDriver = new AltDriver(
            host: "127.0.0.1",
            port: 13000,
            appName: "__default__",
            enableLogging: false,
            connectTimeout: 5
        );

        Reporter.Log("Successfully connected to the game.");
    }

    [AllureAfter("Stop AltTester Driver")]
    protected virtual void StopDriver()
    {
        try
        {
            CommonStuff.AltDriver?.Stop();
            Reporter.Log("Driver stopped successfully");
        }
        catch (Exception ex)
        {
            Reporter.Log($"Error stopping driver: {ex.Message}");
        }
    }
}

public static class CommonStuff
{
    public static AltDriver AltDriver { get; set; }
}