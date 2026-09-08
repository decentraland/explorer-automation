using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace ExplorerAutomation.Tests.Common;

public sealed class PerformanceCapture
{
    private readonly string _root;
    private readonly string _test;
    private readonly DateTimeOffset _start = DateTimeOffset.UtcNow;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly string _csvPath;
    private bool _started;
    private string _error;

    public string MetadataPath { get; }
    public bool Complete { get; private set; }

    public PerformanceCapture(string root, string test)
    {
        _root = root;
        _test = test;
        var directory = Path.Combine(root, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        _csvPath = Path.Combine(directory, "perf.csv");
        MetadataPath = Path.Combine(directory, "test.json");
        WriteMetadata("running", null);
        File.WriteAllText(Path.Combine(root, "active-test.txt"), test);
    }

    public void Begin(Action<string, string> begin)
    {
        try
        {
            // An empty summary path avoids the client's expensive tail-statistics pass.
            begin(_csvPath, "");
            _started = true;
        }
        catch (Exception ex)
        {
            _error = ex.ToString();
            WriteMetadata("capture unavailable", null);
            throw;
        }
    }

    public string Finish(string outcome, bool transportFailed, Action end)
    {
        var testEnd = DateTimeOffset.UtcNow;
        var testDuration = _clock.Elapsed.TotalSeconds;
        if (_started && !transportFailed)
        {
            try { end(); Complete = true; }
            catch (Exception ex) { _error = ex.ToString(); }
        }
        if (transportFailed) _error = "Transport unavailable; End was not called. Buffered frames may be missing.";
        WriteMetadata(outcome, testEnd, testDuration);
        File.WriteAllText(Path.Combine(_root, "active-test.txt"), "between tests");
        var summary = Summarize(ReadCsv(), Complete);
        summary = $"{_test}: {outcome}; test window {testDuration:F2}s; {summary}";
        if (_error != null) summary += $"\nCapture warning: {_error}";
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(_csvPath)!, "report.txt"), summary);
        return summary;
    }

    private void WriteMetadata(string outcome, DateTimeOffset? end, double? duration = null) =>
        File.WriteAllText(MetadataPath, JsonSerializer.Serialize(new
        {
            test = _test, startUtc = _start, endUtc = end, durationSeconds = duration,
            outcome, complete = Complete, error = _error,
            timing = "Host test window includes capture startup and test setup; frames have no UTC timestamps. Capture End follows this window."
        }, new JsonSerializerOptions { WriteIndented = true }));

    public byte[] ReadCsv()
    {
        if (!File.Exists(_csvPath)) return [];
        using var stream = new FileStream(_csvPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    }

    public static string Summarize(byte[] data, bool complete)
    {
        var cpu = new List<double>();
        var gpu = new List<double>();
        var invalid = 0;
        var invalidGpu = 0;
        var text = Encoding.UTF8.GetString(data);
        var lines = text.Split('\n');
        for (var i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;
            // A writer killed halfway through a row must not fabricate a short frame.
            if (i == lines.Length - 1 && !text.EndsWith('\n')) { invalid++; continue; }
            var cells = lines[i].TrimEnd('\r').Split(',');
            if (cells.Length != 3 || !long.TryParse(cells[0], out _)) { invalid++; continue; }
            if (TryTiming(cells[1], out var c)) cpu.Add(c); else invalid++;
            if (TryTiming(cells[2], out var g)) gpu.Add(g); else invalidGpu++;
        }
        return $"{(complete ? "complete" : "PARTIAL/UNAVAILABLE")}; "
            + $"CPU {Metrics(cpu)}; GPU {Metrics(gpu)}; invalid CPU/rows={invalid}, GPU={invalidGpu}. "
            + "Diagnostic only; frame timings do not measure wall-clock hangs.";
    }

    private static bool TryTiming(string value, out double time) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out time)
        && double.IsFinite(time) && time > 0;

    private static string Metrics(List<double> times)
    {
        if (times.Count == 0) return "unavailable (n=0)";
        times.Sort();
        string Percentile(double q, int minimum) => times.Count < minimum ? "n/a" :
            times[(int)Math.Ceiling(times.Count * q) - 1].ToString("F2", CultureInfo.InvariantCulture);
        return $"n={times.Count}, p50={Percentile(.5, 2)}ms, p95={Percentile(.95, 20)}ms, "
            + $"p99={Percentile(.99, 100)}ms, max={times[^1].ToString("F2", CultureInfo.InvariantCulture)}ms, "
            + $">33ms={times.Count(x => x > 33)}, >100ms={times.Count(x => x > 100)}, >1000ms={times.Count(x => x > 1000)}";
    }
}
