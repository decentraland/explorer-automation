using System.Text.Json;
using System.Text.Json.Nodes;
using ExplorerAutomation.Tests.Common;

namespace ExplorerAutomation.CI;

public static class PerformanceSummary
{
    public static JsonObject Build(string root, bool enabled, string scope)
    {
        var cpu = new List<double>();
        var gpu = new List<double>();
        var complete = 0;
        var partial = 0;
        var unavailable = 0;
        var invalidCpu = 0;
        var invalidGpu = 0;
        var result = new JsonObject { ["enabled"] = enabled, ["scope"] = scope };
        if (!enabled) return result;

        // Include started captures without a CSV so missing samples remain visible.
        var directories = Directory.Exists(root)
            ? Directory.EnumerateFiles(root, "perf.csv", SearchOption.AllDirectories)
                .Concat(Directory.EnumerateFiles(root, "test.json", SearchOption.AllDirectories))
                .Select(Path.GetDirectoryName).Distinct().ToArray()
            : [];
        foreach (var directory in directories)
        {
            var csvPath = Path.Combine(directory!, "perf.csv");
            try
            {
                using var stream = new FileStream(csvPath, FileMode.Open, FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete);
                using var copy = new MemoryStream();
                stream.CopyTo(copy);
                var timings = PerformanceCapture.ReadTimings(copy.ToArray());
                invalidCpu += timings.InvalidCpu;
                invalidGpu += timings.InvalidGpu;
                if (timings.Cpu.Count == 0 && timings.Gpu.Count == 0) { unavailable++; continue; }
                cpu.AddRange(timings.Cpu);
                gpu.AddRange(timings.Gpu);
                if (IsComplete(directory!, scope)) complete++; else partial++;
            }
            catch (IOException) { unavailable++; }
            catch (UnauthorizedAccessException) { unavailable++; }
        }
        result["complete"] = complete;
        result["partial"] = partial;
        result["unavailable"] = unavailable;
        result["invalid_cpu_rows"] = invalidCpu;
        result["invalid_gpu"] = invalidGpu;
        result["cpu"] = Metrics(cpu);
        result["gpu"] = Metrics(gpu);
        return result;
    }

    private static bool IsComplete(string directory, string scope)
    {
        try
        {
            if (scope == "fixture")
            {
                var summary = Path.Combine(directory, "perf-summary.txt");
                return File.Exists(summary) && new FileInfo(summary).Length > 0;
            }
            using var metadata = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "test.json")));
            return metadata.RootElement.TryGetProperty("complete", out var complete)
                && complete.ValueKind == JsonValueKind.True;
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
        catch (JsonException) { return false; }
    }

    private static JsonObject Metrics(List<double> samples)
    {
        samples.Sort();
        return new JsonObject
        {
            ["samples"] = samples.Count,
            ["p50_ms"] = PerformanceCapture.Percentile(samples, .5, 2),
            ["p95_ms"] = PerformanceCapture.Percentile(samples, .95, 20),
            ["max_ms"] = samples.Count == 0 ? null : samples[^1]
        };
    }
}
