using AltTester.AltTesterSDK.Driver.Logging;

namespace ExplorerAutomation.Tests;

[SetUpFixture]
public class GlobalSetup
{
    [OneTimeSetUp]
    public void RunBeforeAllTests()
    {
        DotNetEnv.Env.TraversePath().Load();
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

        // The SDK default for the test-side recvall ceiling is 60s. Any
        // WaitFor*/FindObject command whose Player-side `timeout` exceeds that
        // hits CommandResponseTimeoutException before the Unity-side wait
        // resolves — which is exactly what we saw on GH-hosted macos-14 paravirt
        // (SplashScreen.WaitForGone(180) needs ~90s to succeed; the 60s SDK cap
        // fired first). 300s comfortably covers every wait we hand out in
        // EnsureInWorld (Splash 180s, Loading 300s, MainMenu 240s) and leaves
        // headroom for the worst paravirt-VM cold-start without masking a real
        // hang — anything past 5 min is a genuine failure worth surfacing.
        CommonStuff.AltDriver.SetCommandResponseTimeout(300);

        // The Unity-side AltTester server echoes every command/response into
        // Debug.Log → Player.log; at default Debug level WaitFor polling produces
        // multi-KB JSON+stacktrace per iteration and that's ~64% of Player.log
        // bytes on a full InWorld run. Default to Warn for both interactive and
        // CI runs; opt back into Debug via ALT_VERBOSE_LOGS=true when triaging
        // an AltTester-side issue. AltLogger.File keeps its own level (Debug)
        // and writes to AltTester-Server.log next to Player.log — full command
        // history stays available there.
        var verbose = string.Equals(
            Environment.GetEnvironmentVariable("ALT_VERBOSE_LOGS"),
            "true",
            StringComparison.OrdinalIgnoreCase);
        var unityLogLevel = verbose ? AltLogLevel.Debug : AltLogLevel.Warn;
        CommonStuff.AltDriver.SetServerLogging(AltLogger.Unity, unityLogLevel);
        Reporter.Log($"AltTester server Unity log level: {unityLogLevel}" + (verbose ? " (ALT_VERBOSE_LOGS=true)" : ""));

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