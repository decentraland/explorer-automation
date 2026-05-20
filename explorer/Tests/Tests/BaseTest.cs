namespace ExplorerAutomation.Tests.Tests;

[TestFixture]
[AllureNUnit]
public abstract class BaseTest
{
    private const string PERF_ENV = "EXPLORER_PERF_RECORD";

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
        var dir = Path.Combine(Path.GetTempPath(), "explorer-perf", fixtureName);
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

        LogInlinePerfTable(_perfCsvPath);

        AllureApi.AddAttachment("perf-summary.txt", "text/plain", File.ReadAllBytes(_perfSummaryPath));
        if (File.Exists(_perfCsvPath))
            AllureApi.AddAttachment("perf.csv", "text/csv", File.ReadAllBytes(_perfCsvPath));
    }

    private static void LogInlinePerfTable(string csvPath)
    {
        // Parse the raw per-frame CSV and emit avg + a percentile distribution
        // as Allure steps so reviewers can read the shape of the run without
        // opening the attachment. PerfSampler's 8-line perf-summary.txt
        // (1%/0.1%/worst from the GamersNexus method) is still attached for
        // byte-identical AutoPilot parity, but the chassis windows are too
        // short (~50-500 samples) for sub-percent buckets to mean anything;
        // a p5/p25/p50/p75/p95 spread describes the actual distribution.
        if (!File.Exists(csvPath))
        {
            Reporter.Log("Perf: perf.csv missing on disk — skipping inline percentile table");
            return;
        }

        var cpu = new List<double>();
        var gpu = new List<double>();
        using (var reader = new StreamReader(csvPath))
        {
            reader.ReadLine(); // discard "Frame","CPU Time","GPU Time" header
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                var parts = line.Split(',');
                if (parts.Length < 3) continue;
                if (double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var c)) cpu.Add(c);
                if (double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var g)) gpu.Add(g);
            }
        }

        if (cpu.Count == 0 || gpu.Count == 0)
        {
            Reporter.Log($"Perf: perf.csv had no parseable rows (CPU={cpu.Count}, GPU={gpu.Count})");
            return;
        }

        cpu.Sort();
        gpu.Sort();
        LogPerfTables(cpu, gpu);
    }

    private static void LogPerfTables(List<double> cpu, List<double> gpu)
    {
        // Two CPU/GPU tables emitted as 6 separate Allure steps (3 rows per
        // table). One step per row keeps each line distinct in the report —
        // Allure collapses '\n' inside a single step name into a space, so
        // tables built with multi-line strings render as one mangled line.
        // Cell padding uses U+00A0 (non-breaking space) which the HTML
        // renderer doesn't collapse, unlike ASCII spaces.
        var nLabel = $"N = {cpu.Count}";
        var labelW = Math.Max(nLabel.Length, 3); // accommodate "CPU"/"GPU"

        Reporter.Log(Row(labelW, nLabel, "avg (ms)", "p50 (ms)"));
        Reporter.Log(Row(labelW, "CPU", Num(Avg(cpu)), Num(Percentile(cpu, 0.50))));
        Reporter.Log(Row(labelW, "GPU", Num(Avg(gpu)), Num(Percentile(gpu, 0.50))));

        Reporter.Log(Row(labelW, nLabel, "p5 (ms)", "p25 (ms)", "p75 (ms)", "p95 (ms)"));
        Reporter.Log(Row(labelW, "CPU",
            Num(Percentile(cpu, 0.05)), Num(Percentile(cpu, 0.25)),
            Num(Percentile(cpu, 0.75)), Num(Percentile(cpu, 0.95))));
        Reporter.Log(Row(labelW, "GPU",
            Num(Percentile(gpu, 0.05)), Num(Percentile(gpu, 0.25)),
            Num(Percentile(gpu, 0.75)), Num(Percentile(gpu, 0.95))));
    }

    private const char NBSP = ' ';
    private const int CELL_WIDTH = 8;

    private static string Row(int labelWidth, string label, params string[] cells)
    {
        var parts = new string[cells.Length + 1];
        parts[0] = PadRight(label, labelWidth);
        for (var i = 0; i < cells.Length; i++) parts[i + 1] = PadRight(cells[i], CELL_WIDTH);
        return string.Join($"{NBSP}|{NBSP}", parts);
    }

    private static string Num(double v) =>
        v.ToString("F2", CultureInfo.InvariantCulture);

    private static string PadRight(string s, int width) =>
        s.Length >= width ? s : s + new string(NBSP, width - s.Length);

    private static double Avg(List<double> values)
    {
        double sum = 0;
        for (var i = 0; i < values.Count; i++) sum += values[i];
        return sum / values.Count;
    }

    private static double Percentile(List<double> sorted, double fraction)
    {
        // Linear interpolation between closest ranks (NumPy "linear" default).
        if (sorted.Count == 1) return sorted[0];
        var rank = fraction * (sorted.Count - 1);
        var lo = (int)Math.Floor(rank);
        var hi = (int)Math.Ceiling(rank);
        return lo == hi
            ? sorted[lo]
            : sorted[lo] + (sorted[hi] - sorted[lo]) * (rank - lo);
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
        if (Views.SplashScreen.IsPresent())
        {
            Reporter.Log("Splash screen detected — waiting for it to clear");
            Views.SplashScreen.WaitForGone(60);
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

        Views.MainMenu.WaitFor(120);

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
