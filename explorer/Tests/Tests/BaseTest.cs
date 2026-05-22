namespace ExplorerAutomation.Tests.Tests;

[TestFixture]
[AllureNUnit]
public abstract class BaseTest
{
    private const string PERF_ENV = "EXPLORER_PERF_RECORD";
    private const string PERF_DIR_ENV = "EXPLORER_PERF_DIR";

    protected Exception ExceptionFromOneTimeSetUp;

    private string _perfCsvPath;
    private string _perfSummaryPath;
    private bool _perfStarted;

    protected ViewContainer Views => ViewContainer.Instance;
    protected AltDriver AltDriver => CommonStuff.AltDriver;

    #region Setup and Teardown

    [OneTimeSetUp]
    [AllureBefore("Ensure the player is in world")]
    public void OneTimeSetUp()
    {
        try
        {
            EnsureInWorld();
        }
        catch (Exception ex)
        {
            // Capture and rethrow per-test in [SetUp]. NUnit reports OneTimeSetUp failures
            // at the fixture level, which Allure doesn't render as test entries — so all
            // tests in this fixture would be invisible in the report. By re-failing inside
            // [SetUp], each test gets its own entry marked as failed.
            ExceptionFromOneTimeSetUp = ex;
            throw;
        }

        // Opt-in fixture-level perf capture. Driven by EXPLORER_PERF_RECORD=1 set by
        // the chassis workflow; local runs leave it unset and skip the AutoPilot
        // PerfSampler call entirely so non-CI builds without the perf module loaded
        // don't blow up.
        if (Environment.GetEnvironmentVariable(PERF_ENV) != "1") return;

        var fixtureName = TestContext.CurrentContext.Test.ClassName ?? "unknown-fixture";
        // EXPLORER_PERF_DIR (set by chassis workflow) anchors output to a stable,
        // upload-artifact-friendly path (e.g. $RUNNER_TEMP/explorer-perf). This way the
        // CSV is collected by the workflow's upload-artifact step even if the test process
        // is killed (timeout, crash) before [OneTimeTearDown] AttachPerf runs and can call
        // AllureApi.AddAttachment. Local runs leave the var unset and fall back to %TEMP%.
        var perfRoot = Environment.GetEnvironmentVariable(PERF_DIR_ENV);
        if (string.IsNullOrEmpty(perfRoot))
            perfRoot = Path.Combine(Path.GetTempPath(), "explorer-perf");
        var dir = Path.Combine(perfRoot, fixtureName);
        Directory.CreateDirectory(dir);
        _perfCsvPath = Path.Combine(dir, "perf.csv");
        _perfSummaryPath = Path.Combine(dir, "perf-summary.txt");

        AltDriver.CallStaticMethod<string>(
            "DCL.PerformanceAndDiagnostics.AutoPilot.PerfSampler", "Begin",
            "DCL.Diagnostics.AutoPilot",
            new object[] { _perfCsvPath, _perfSummaryPath });

        _perfStarted = true;
        Reporter.Log($"PerfSampler.Begin -> {_perfSummaryPath}");
    }

    [OneTimeTearDown]
    [AllureAfter("Attach perf summary")]
    public void AttachPerf()
    {
        if (!_perfStarted) return;

        try
        {
            AltDriver.CallStaticMethod<string>(
                "DCL.PerformanceAndDiagnostics.AutoPilot.PerfSampler", "End",
                "DCL.Diagnostics.AutoPilot",
                new object[] { });
        }
        catch (Exception ex)
        {
            // AltTester wraps the Player-side exception in a TargetInvocationException;
            // ex.Message is the unhelpful "Exception has been thrown by the target of an
            // invocation." Log ToString() so the inner trace from AltTester's error
            // payload makes it into the Allure report.
            Reporter.Log($"PerfSampler.End call failed (Player crash?):\n{ex}");

            // Best-effort: PerfSampler closes the CSV BEFORE writing the summary, so a
            // summary-side crash still leaves a complete perf.csv on disk. Attach what
            // we have so a chassis run with a broken summary writer is still
            // post-mortemable.
            if (File.Exists(_perfCsvPath))
                AllureApi.AddAttachment("perf.csv", "text/csv", File.ReadAllBytes(_perfCsvPath));
            return;
        }

        if (!File.Exists(_perfSummaryPath))
            throw new AssertionException($"PerfSampler.End did not produce summary at {_perfSummaryPath}");

        // Attach raw data only. The perf window is fixture-level
        // (one OneTimeSetUp -> N test methods -> OneTimeTearDown) but Allure
        // renders attachments per-test, so any inline rendering here would
        // duplicate the same numbers across every test in the fixture and
        // misleadingly imply per-test granularity. Downstream analysis
        // (CV across runs, baseline comparison, custom dashboards) consumes
        // perf.csv directly from the artifact bundle.
        AllureApi.AddAttachment("perf-summary.txt", "text/plain", File.ReadAllBytes(_perfSummaryPath));
        if (File.Exists(_perfCsvPath))
            AllureApi.AddAttachment("perf.csv", "text/csv", File.ReadAllBytes(_perfCsvPath));
    }

    [SetUp]
    [AllureBefore("Set up before each test")]
    public void SetUp()
    {
        if (ExceptionFromOneTimeSetUp != null)
        {
            Reporter.Log($"Fixture OneTimeSetUp failed earlier: {ExceptionFromOneTimeSetUp.Message}");
            throw new AssertionException(
                $"Fixture OneTimeSetUp failed: {ExceptionFromOneTimeSetUp.Message}",
                ExceptionFromOneTimeSetUp);
        }

        Reporter.Log($"Starting test: {TestContext.CurrentContext.Test.Name}");

        // In case a popup is opened, this will close it.
        // Skip when sitting on the auth screen — Escape there can exit/transition.
        if (!Views.AuthenticationMainScreen.IsPresent())
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
    protected virtual void EnsureInWorld()
    {
        // MinimumSpecsScreen overlays the splash on hardware that doesn't meet
        // the recommended specs (CI 4-vCPU runner). The modal is instantiated
        // late in MainSceneLoader bootstrap (VerifyMinimumHardwareRequirementMet),
        // well after AltTester connects — so we must poll until either the modal
        // appears or splash clears (which means specs were met / no modal coming).
        DismissMinimumSpecsModalIfPresent();

        // Wait for any splash to clear first — both the cached-account flow and the
        // auto-login (token-bridge) flow start with the splash, but they diverge after.
        // 180s ceiling (was 60s): GH-hosted macos-14 is a 3-core Apple Paravirtual VM
        // that fails all three MinimumSpecs checks (GPU/VRAM/RAM); Catalyst→realm→
        // first-frame on that hardware reliably takes 50-90s vs ~5-15s on real DevBox.
        // Local runs on adequate hardware return well before the cap, so the bump is
        // free for normal use; only the CI VM ever actually waits this long.
        if (Views.SplashScreen.IsPresent())
        {
            Reporter.Log("Splash screen detected — waiting for it to clear");
            Views.SplashScreen.WaitForGone(180);
        }

        // After splash, three legitimate states are possible:
        //   a) Cached-account auth screen (Jump Into Decentraland button).
        //      Happens when the Explorer has a Thirdweb cache but no token bridge.
        //   b) No auth screen at all (auto-login via auth-token-bridge.txt or already
        //      in-world). Happens for the metaforge `account login --auto-login` flow,
        //      and for tests that follow another fixture that ended in-world.
        //   c) Loading screen still up (post-JumpIn or post-auto-login transition).
        if (Views.AuthenticationMainScreen.IsPresent())
        {
            Reporter.Log("Cached-account auth screen detected — clicking Jump Into Decentraland");
            Views.AuthenticationMainScreen.JumpIntoWorldButton.Click();
        }
        else
        {
            Reporter.Log("No auth screen — auto-login via token bridge or already in-world");
        }

        // CRITICAL: wait for the SceneLoadingScreen (the 0→100% progress bar) to appear AND
        // then disappear. Until it disappears, the world is still streaming and sidebar
        // shortcuts get silently dropped. We can't use IsPresent() here — Unity instantiates
        // the loading screen asynchronously after JumpIntoWorld, so a 0ms check returns false
        // before it shows up. Instead, give it ~15s to appear; if it doesn't, assume world
        // was already loaded; if it does, wait up to 5 min for it to finish.
        try
        {
            Views.LoadingScreen.WaitFor(15);
            Reporter.Log("Scene loading screen visible — waiting for world streaming to finish (up to 5 min)");
            Views.LoadingScreen.WaitForGone(300);
            Reporter.Log("Scene loading complete — HUD should now be interactable");
        }
        catch (Exception)
        {
            Reporter.Log("Scene loading screen never appeared — assuming world was already loaded");
        }

        // 240s (was 120s): same reason as the SplashScreen bump above — bootstrap
        // tail on GH-hosted macos-14 paravirt can drag well past 2 min on a cold
        // load (asset bundle warmup, comms handshake, profile fetch). Real
        // hardware hits MainMenu in ~10-30s and never approaches this ceiling.
        Views.MainMenu.WaitFor(240);

        // The SidebarController subscribes its onClick listeners in OnViewInstantiated,
        // which fires asynchronously after the SidebarView GameObject appears in the scene.
        // The first sidebar click / shortcut after EnsureInWorld returns can land in that
        // gap and get silently dropped. There's no public signal for when subscriptions
        // are wired, so we settle for a fixed wait. Empirically ~20s is enough for the
        // first test method of the first in-world fixture; subsequent fixtures don't need
        // it (the system is already warm) but the cost is small.
        Thread.Sleep(20_000);
        Reporter.Log("Player is in-world and main menu is ready");
    }

    [AllureStep("Dismiss the MinimumSpecs warning modal if present")]
    private void DismissMinimumSpecsModalIfPresent()
    {
        // The modal is instantiated late in bootstrap (after splash is up). Poll
        // until either the modal appears (click Continue and exit) or splash
        // clears (bootstrap passed the specs check without showing a modal).
        // Hard cap at 90s so a stuck Explorer doesn't pin us here forever.
        var deadline = DateTime.UtcNow.AddSeconds(90);
        while (DateTime.UtcNow < deadline)
        {
            if (Views.MinimumSpecsScreen.IsPresent())
            {
                Reporter.Log("MinimumSpecs warning modal detected — clicking Continue");
                Views.MinimumSpecsScreen.ContinueButton.Click();
                return;
            }
            if (!Views.SplashScreen.IsPresent())
            {
                Reporter.Log("Splash cleared before MinimumSpecs modal appeared — specs met");
                return;
            }
            Thread.Sleep(500);
        }
        Reporter.Log("Timed out (90s) waiting for either MinimumSpecs modal or splash to clear");
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
