using System.Text;
using System.Text.Json;
using ExplorerAutomation.Tests.Common;
using NUnit.Framework;

public class CaptureTests
{
    private string root;
    [SetUp] public void Setup() => root = Path.Combine(Path.GetTempPath(), "perf-tests-" + Guid.NewGuid().ToString("N"));
    [TearDown] public void Cleanup() { if (Directory.Exists(root)) Directory.Delete(root, true); }
    private static byte[] Csv(string rows) => Encoding.UTF8.GetBytes("Frame,CPU Time,GPU Time\n" + rows);

    [Test] public void LostTransportPreservesPartialCaptureWithoutCallingEnd()
    {
        var capture = new PerformanceCapture(root, "failed test");
        capture.Begin((csv, _) => File.WriteAllBytes(csv, Csv("1,1500,20\n2,16,")));
        var called = false;
        var report = capture.Finish("Failed", true, () => called = true);
        Assert.That(called, Is.False);
        Assert.That(capture.Complete, Is.False);
        Assert.That(report, Does.Contain("PARTIAL/UNAVAILABLE"));
        Assert.That(report, Does.Contain(">1000ms=1"));
        Assert.That(report, Does.Contain("invalid CPU/rows=1"));
        using var metadata = JsonDocument.Parse(File.ReadAllText(capture.MetadataPath));
        Assert.That(metadata.RootElement.GetProperty("outcome").GetString(), Is.EqualTo("Failed"));
        Assert.That(metadata.RootElement.GetProperty("endUtc").ValueKind, Is.EqualTo(JsonValueKind.String));
    }

    [Test] public void FailedEndIsDiagnosticAndKeepsExistingFrames()
    {
        var capture = new PerformanceCapture(root, "test");
        capture.Begin((csv, _) => File.WriteAllBytes(csv, Csv("1,16,4\n")));
        var report = capture.Finish("Passed", false, () => throw new IOException("disconnected"));
        Assert.That(report, Does.Contain("disconnected"));
        Assert.That(capture.ReadCsv(), Is.Not.Empty);
        Assert.That(capture.Complete, Is.False);
    }

    [Test] public void FailedBeginNeverCallsEnd()
    {
        var capture = new PerformanceCapture(root, "test");
        Assert.Throws<InvalidOperationException>(() => capture.Begin((_, _) => throw new InvalidOperationException("missing sampler")));
        var report = capture.Finish("Passed", false, () => Assert.Fail("End must not run"));
        Assert.That(report, Does.Contain("missing sampler"));
        Assert.That(report, Does.Contain("unavailable (n=0)"));
    }

    [Test] public void RepeatedNamesHaveSeparateArtifacts()
    {
        var first = new PerformanceCapture(root, "same test");
        var second = new PerformanceCapture(root, "same test");
        Assert.That(first.MetadataPath, Is.Not.EqualTo(second.MetadataPath));
    }

    [Test] public void SuccessfulEndFlushesBeforeReporting()
    {
        var capture = new PerformanceCapture(root, "test");
        string path = null;
        capture.Begin((csv, summary) => { path = csv; Assert.That(summary, Is.Empty); });
        var report = capture.Finish("Passed", false, () => File.WriteAllBytes(path, Csv("1,20,5\n2,30,6\n")));
        Assert.That(capture.Complete, Is.True);
        Assert.That(report, Does.Contain("CPU n=2, p50=20.00ms"));
        Assert.That(File.ReadAllText(Path.Combine(root, "active-test.txt")), Is.EqualTo("between tests"));
    }

    [Test] public void InvalidAndMissingGpuSamplesAreNotFastFrames()
    {
        var report = PerformanceCapture.Summarize(Csv("1,0,0\n2,NaN,-1\n3,16,0\n4,Infinity,NaN\n"), true);
        Assert.That(report, Does.Contain("CPU n=1"));
        Assert.That(report, Does.Contain("GPU unavailable (n=0)"));
        Assert.That(report, Does.Contain("invalid CPU/rows=3, GPU=4"));
        Assert.That(report, Does.Contain("p99=n/a"));
    }

    [Test] public void TailPercentilesRequireEnoughSamples()
    {
        var rows = string.Concat(Enumerable.Range(1, 100).Select(i => $"{i},{i},5\n"));
        var report = PerformanceCapture.Summarize(Csv(rows), true);
        Assert.That(report, Does.Contain("p95=95.00ms, p99=99.00ms"));
        Assert.That(report, Does.Contain(">33ms=67, >100ms=0"));
    }

    [Test] public void StartupMarkerSurvivesAnInterruptedTest()
    {
        var capture = new PerformanceCapture(root, "test/with parameters");
        using var json = JsonDocument.Parse(File.ReadAllText(capture.MetadataPath));
        Assert.That(json.RootElement.GetProperty("outcome").GetString(), Is.EqualTo("running"));
        Assert.That(json.RootElement.GetProperty("complete").GetBoolean(), Is.False);
        Assert.That(File.ReadAllText(Path.Combine(root, "active-test.txt")), Is.EqualTo("test/with parameters"));
    }
}
