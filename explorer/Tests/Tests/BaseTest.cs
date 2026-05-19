namespace ExplorerAutomation.Tests.Tests;

[TestFixture]
[AllureNUnit]
public abstract class BaseTest
{
    // Decentraland Explorer's Application.persistentDataPath on Windows.
    // Track B (Slice 4) sink writes perf-<UTC-timestamp>.json files here when
    // launched with -perfRecord or -alttester (the latter applies to every
    // chassis run, so the file is always present for these fixtures).
    private static readonly string EXPLORER_PERSISTENT_DATA_PATH = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "AppData", "LocalLow", "Decentraland", "Explorer");

    protected Exception ExceptionFromOneTimeSetUp;

    protected ViewContainer Views => ViewContainer.Instance;
    protected AltDriver AltDriver => CommonStuff.AltDriver;

    private string _perfPayloadPath;
    private long _perfPayloadStartOffset;

    #region Setup and Teardown

    [OneTimeSetUp]
    [AllureBefore("Ensure the player is in world")]
    public void OneTimeSetUp()
    {
        // Snapshot the perf-payload sink offset BEFORE we drive any user flow, so
        // OneTimeTearDown attaches only the slice emitted during this fixture's
        // lifetime. One Explorer process services every fixture in a `dotnet test`
        // run, so the file accumulates across fixtures; the offset trick gives us
        // per-fixture slicing without requiring an extra timestamp field in the
        // sink payload (would force a new unity-explorer build).
        CapturePerfPayloadOffset();

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
    }

    [OneTimeTearDown]
    [AllureAfter("Attach perf-payload slice")]
    public void AttachPerfPayloadSlice()
    {
        if (_perfPayloadPath == null) return;
        try
        {
            if (!File.Exists(_perfPayloadPath)) return;
            var length = new FileInfo(_perfPayloadPath).Length;
            if (length <= _perfPayloadStartOffset) return;

            using var fs = new FileStream(_perfPayloadPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            fs.Seek(_perfPayloadStartOffset, SeekOrigin.Begin);
            using var ms = new MemoryStream();
            fs.CopyTo(ms);
            var bytes = ms.ToArray();
            if (bytes.Length == 0) return;

            // application/x-ndjson is the conventional MIME type for newline-delimited JSON.
            // Allure has no built-in renderer for it, but the file extension keeps it
            // round-trippable through `jq -s` / `aggregate_inworld_cv.py` (Slice 5).
            AllureApi.AddAttachment("perf-payloads.jsonl", "application/x-ndjson", bytes);
            Reporter.Log($"Attached perf-payload slice ({bytes.Length} bytes) from {_perfPayloadPath}");
        }
        catch (Exception ex)
        {
            Reporter.Log($"Failed to attach perf-payload slice (non-fatal): {ex.Message}");
        }
    }

    private void CapturePerfPayloadOffset()
    {
        try
        {
            if (!Directory.Exists(EXPLORER_PERSISTENT_DATA_PATH)) return;

            // The sink names files perf-<UTC-timestamp>.json — one per Explorer launch.
            // Track the most recently written one; if Explorer was restarted between
            // fixtures, this picks up the new file automatically.
            FileInfo latest = null;
            foreach (var path in Directory.EnumerateFiles(EXPLORER_PERSISTENT_DATA_PATH, "perf-*.json"))
            {
                var info = new FileInfo(path);
                if (latest == null || info.LastWriteTimeUtc > latest.LastWriteTimeUtc)
                    latest = info;
            }

            if (latest == null) return;

            _perfPayloadPath = latest.FullName;
            _perfPayloadStartOffset = latest.Length;
            Reporter.Log($"Perf-payload sink: tracking {_perfPayloadPath} from offset {_perfPayloadStartOffset}");
        }
        catch (Exception ex)
        {
            Reporter.Log($"Failed to locate perf-payload sink (non-fatal): {ex.Message}");
        }
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
