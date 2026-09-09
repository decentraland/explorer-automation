using System.Text.Json.Nodes;
using ExplorerAutomation.CI;
using NUnit.Framework;

[TestFixture]
public class SummaryTests
{
    private string root;
    [SetUp] public void SetUp() => root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    [TearDown] public void TearDown() { if (Directory.Exists(root)) Directory.Delete(root, true); }

    private string Capture(string name, string rows, bool complete)
    {
        var directory = Path.Combine(root, name);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "test.json"), "{\"complete\":" + (complete ? "true" : "false") + "}");
        if (rows != null) File.WriteAllText(Path.Combine(directory, "perf.csv"), "Frame,CPU Time,GPU Time\n" + rows);
        return directory;
    }

    [Test] public void PoolsFramesInsteadOfAveragingCaptureMedians()
    {
        Capture("short", "1,1000,50\n2,1000,50\n", true);
        Capture("long", string.Concat(Enumerable.Range(1, 20).Select(i => $"{i},10,2\n")), true);
        var report = PerformanceSummary.Build(root, true, "test");
        Assert.That(report["cpu"]!["samples"]!.GetValue<int>(), Is.EqualTo(22));
        Assert.That(report["cpu"]!["p50_ms"]!.GetValue<double>(), Is.EqualTo(10));
        Assert.That(report["cpu"]!["p95_ms"]!.GetValue<double>(), Is.EqualTo(1000));
        Assert.That(report["gpu"]!["p50_ms"]!.GetValue<double>(), Is.EqualTo(2));
        Assert.That(report["complete"]!.GetValue<int>(), Is.EqualTo(2));
    }

    [Test] public void PreservesLargePartialFrameAndRejectsTruncatedTail()
    {
        Capture("interrupted", "1,86284.59,17.35\n2,0,NaN\n3,12,", false);
        Capture("never-started", null, false);
        var report = PerformanceSummary.Build(root, true, "test");
        Assert.That(report["cpu"]!["max_ms"]!.GetValue<double>(), Is.EqualTo(86284.59));
        Assert.That(report["cpu"]!["samples"]!.GetValue<int>(), Is.EqualTo(1));
        Assert.That(report["cpu"]!["p50_ms"], Is.Null);
        Assert.That(report["cpu"]!["p95_ms"], Is.Null);
        Assert.That(report["partial"]!.GetValue<int>(), Is.EqualTo(1));
        Assert.That(report["unavailable"]!.GetValue<int>(), Is.EqualTo(1));
        Assert.That(report["invalid_cpu_rows"]!.GetValue<int>(), Is.EqualTo(2));
    }

    [Test] public void MissingGpuIsUnavailableRatherThanZeroMilliseconds()
    {
        Capture("cpu-only", "1,20,0\n2,30,-1\n3,40,Infinity\n", true);
        var report = PerformanceSummary.Build(root, true, "test");
        Assert.That(report["cpu"]!["p50_ms"]!.GetValue<double>(), Is.EqualTo(30));
        Assert.That(report["gpu"]!["samples"]!.GetValue<int>(), Is.Zero);
        Assert.That(report["gpu"]!["max_ms"], Is.Null);
    }

    [Test] public void DisabledRecordingIgnoresStaleFiles()
    {
        Capture("stale", "1,10,2\n", true);
        var report = PerformanceSummary.Build(root, false, "test");
        Assert.That(report["enabled"]!.GetValue<bool>(), Is.False);
        Assert.That(report["cpu"], Is.Null);
    }

    [Test] public void MissingRootDoesNotInventMeasurements()
    {
        var report = PerformanceSummary.Build(root, true, "fixture");
        Assert.That(report["complete"]!.GetValue<int>(), Is.Zero);
        Assert.That(report["cpu"]!["samples"]!.GetValue<int>(), Is.Zero);
        Assert.That(report["cpu"]!["p50_ms"], Is.Null);
    }

    [Test] public void MacFixtureCompletionRequiresNonemptySummary()
    {
        var finished = Capture("finished", "1,10,2\n", false);
        File.WriteAllText(Path.Combine(finished, "perf-summary.txt"), "complete summary");
        var interrupted = Capture("interrupted", "1,20,4\n", true);
        File.WriteAllText(Path.Combine(interrupted, "perf-summary.txt"), "");
        var report = PerformanceSummary.Build(root, true, "fixture");
        Assert.That(report["scope"]!.GetValue<string>(), Is.EqualTo("fixture"));
        Assert.That(report["complete"]!.GetValue<int>(), Is.EqualTo(1));
        Assert.That(report["partial"]!.GetValue<int>(), Is.EqualTo(1));
    }

    [Test] public void CorruptMetadataDoesNotDiscardValidPartialSamples()
    {
        var directory = Capture("broken", "1,20,4\n2,30,5\n", true);
        File.WriteAllText(Path.Combine(directory, "test.json"), "{truncated");
        var report = PerformanceSummary.Build(root, true, "test");
        Assert.That(report["partial"]!.GetValue<int>(), Is.EqualTo(1));
        Assert.That(report["cpu"]!["samples"]!.GetValue<int>(), Is.EqualTo(2));
    }
}
