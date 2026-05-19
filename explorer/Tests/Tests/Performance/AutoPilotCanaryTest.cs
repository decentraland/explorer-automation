using System.Diagnostics;
using System.Globalization;
using System.Linq;

namespace ExplorerAutomation.Tests.Tests.Performance;

// AutoPilot canary — Track A (Slice 2) of the runtime perf initiative.
//
// Launches the instrumented Standalone Player as a subprocess with the existing
// product flags (`--autopilot --csv ... --summary ...`) defined in
// unity-explorer's AppArgsFlags. AutoPilot.cs drives its own lifecycle (waits
// for LoadingStatus.Completed, samples 90 s at spawn, writes a plain-text
// summary, Application.Quit) so this fixture is launcher-glue only: spawn,
// wait for exit, assert summary shape, attach CSV + summary to Allure.
//
// Does NOT inherit BaseTest and does NOT use AltTester — AutoPilot has no
// AltTester dependency. The chassis logs Explorer in via `mf account login`
// before this fixture runs; we trust that pre-condition.
[TestFixture]
[AllureNUnit]
public class AutoPilotCanaryTest
{
    private static readonly string[] EXPECTED_SUMMARY_LABELS =
    {
        "CPU average",
        "CPU 1% worst",
        "CPU 0.1% worst",
        "CPU worst",
        "GPU average",
        "GPU 1% worst",
        "GPU 0.1% worst",
        "GPU worst",
    };

    private const int AUTOPILOT_TIMEOUT_SECONDS = 180;
    private const string EXPLORER_BIN_ENV = "EXPLORER_BIN_PATH";

    private string _workDir;
    private string _csvPath;
    private string _summaryPath;
    private string _stdoutPath;
    private string _stderrPath;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        var tag = "autopilot-" + Guid.NewGuid().ToString("N").Substring(0, 8);
        _workDir = Path.Combine(Path.GetTempPath(), tag);
        Directory.CreateDirectory(_workDir);
        _csvPath = Path.Combine(_workDir, "perf.csv");
        _summaryPath = Path.Combine(_workDir, "perf-summary.txt");
        _stdoutPath = Path.Combine(_workDir, "autopilot-stdout.log");
        _stderrPath = Path.Combine(_workDir, "autopilot-stderr.log");
    }

    [Test]
    [Category("Performance")]
    public void StandAtSpawn_90s()
    {
        var binPath = ResolveExplorerBinaryPath();
        Reporter.Log($"AutoPilot canary launching: {binPath}");
        Reporter.Log($"  --csv     = {_csvPath}");
        Reporter.Log($"  --summary = {_summaryPath}");

        var psi = new ProcessStartInfo
        {
            FileName = binPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = false,
            WorkingDirectory = Path.GetDirectoryName(binPath)!,
        };
        psi.ArgumentList.Add("--autopilot");
        psi.ArgumentList.Add("--csv");
        psi.ArgumentList.Add(_csvPath);
        psi.ArgumentList.Add("--summary");
        psi.ArgumentList.Add(_summaryPath);

        using var stdoutWriter = new StreamWriter(_stdoutPath);
        using var stderrWriter = new StreamWriter(_stderrPath);
        using var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
        proc.OutputDataReceived += (_, e) => { if (e.Data != null) stdoutWriter.WriteLine(e.Data); };
        proc.ErrorDataReceived += (_, e) => { if (e.Data != null) stderrWriter.WriteLine(e.Data); };

        if (!proc.Start())
            Assert.Fail($"Failed to start Explorer binary at {binPath}");

        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();

        var exited = proc.WaitForExit(AUTOPILOT_TIMEOUT_SECONDS * 1000);
        if (!exited)
        {
            Reporter.Log($"AutoPilot did not exit within {AUTOPILOT_TIMEOUT_SECONDS}s — killing process tree.");
            try { proc.Kill(entireProcessTree: true); }
            catch (Exception ex) { Reporter.Log($"Kill failed: {ex.Message}"); }
            proc.WaitForExit(5_000);
            Assert.Fail($"Explorer did not finish AutoPilot within {AUTOPILOT_TIMEOUT_SECONDS}s.");
        }

        Reporter.Log($"AutoPilot exited with code {proc.ExitCode}.");
        Assert.That(proc.ExitCode, Is.EqualTo(0),
            "Explorer exited with non-zero code; see stdout/stderr Allure attachments.");
        Assert.That(File.Exists(_summaryPath),
            $"Expected AutoPilot summary file at {_summaryPath}.");

        var summaryLines = File.ReadAllLines(_summaryPath);
        Reporter.Log("AutoPilot summary:");
        foreach (var line in summaryLines)
            Reporter.Log("  " + line);

        var missing = new List<string>();
        var unparsable = new List<string>();

        foreach (var label in EXPECTED_SUMMARY_LABELS)
        {
            var match = summaryLines.FirstOrDefault(
                l => l.StartsWith(label + ":", StringComparison.Ordinal));

            if (match == null)
            {
                missing.Add(label);
                continue;
            }

            var valueStr = match.Substring(label.Length + 1).Trim();
            if (!double.TryParse(valueStr, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                unparsable.Add($"{label}=({valueStr})");
        }

        Assert.That(missing, Is.Empty, $"Missing summary labels: {string.Join(", ", missing)}");
        Assert.That(unparsable, Is.Empty, $"Unparsable summary values: {string.Join(", ", unparsable)}");
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        TryAttach(_summaryPath, "perf-summary.txt");
        TryAttach(_csvPath, "perf.csv");
        TryAttach(_stdoutPath, "autopilot-stdout.log");
        TryAttach(_stderrPath, "autopilot-stderr.log");
    }

    private static void TryAttach(string path, string name)
    {
        if (!string.IsNullOrEmpty(path) && File.Exists(path))
            Reporter.AttachFileToAllure(path, name);
    }

    private static string ResolveExplorerBinaryPath()
    {
        var env = Environment.GetEnvironmentVariable(EXPLORER_BIN_ENV);
        if (!string.IsNullOrWhiteSpace(env))
        {
            if (File.Exists(env)) return env;
            Reporter.Log($"{EXPLORER_BIN_ENV} is set ('{env}') but the file is missing — falling back to default locations.");
        }

        var candidates = new[]
        {
            // mf explorer install default on Windows.
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Decentraland", "Explorer", "Decentraland.exe"),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Decentraland", "MetaForge", "explorer", "current", "Decentraland.exe"),
        };

        foreach (var c in candidates)
            if (File.Exists(c)) return c;

        Assert.Fail(
            $"Could not locate Decentraland.exe. Set {EXPLORER_BIN_ENV} to the binary path. " +
            $"Tried: {string.Join("; ", candidates)}.");
        return null!;
    }
}
