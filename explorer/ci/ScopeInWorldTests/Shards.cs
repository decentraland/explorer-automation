using System.Text.Json;
using System.Text.Json.Nodes;

namespace ExplorerAutomation.Ci.ScopeInWorldTests;

/// <summary>
/// Splits the resolved fixtures across runners and writes the plan as a GitHub matrix.
/// </summary>
// The fixture is the unit and never splits: intra-fixture [Order] is load-bearing
// (BackpackOutfitsTests is save -> equip -> delete). Splitting whole fixtures is safe because
// NUnit re-applies fixture-level [Order] inside whatever subset a shard runs.
internal static class Shards
{
    /// <summary>
    /// Selections at or below this many tests run on one runner: a second shard has to acquire
    /// a runner, install the Explorer and provision an account before its first test, and a
    /// small scope pays that in full for a saving smaller than it.
    /// </summary>
    // At ~19s a test, splitting only wins once half the test time beats the second runner's
    // startup: 2 x wait / 19. That is ~19 tests against a cold GPU runner (~3 min) and ~4
    // against a warm one (~37s), so the break-even sits between them rather than at either.
    internal const int MinTestsToSplit = 15;

    internal static List<Shard> Plan(ReachabilityGraph graph, IReadOnlyList<string> fixtures, int count)
    {
        var bins = Math.Min(count, fixtures.Count);
        var tests = fixtures.Sum(f => graph.TestCounts.GetValueOrDefault(f));

        // One shard carries everything rather than risk an uneven or empty split, or spend
        // more on startup than the split saves. Slower is the only acceptable failure here.
        if (bins < 2 || tests <= MinTestsToSplit || graph.CountsUntrusted is not null)
            return [Shard.Of(1, 1, graph, fixtures)];

        var buckets = new List<List<string>>();
        var weights = new int[bins];
        for (var i = 0; i < bins; i++)
            buckets.Add([]);

        // Longest-processing-time-first: heaviest fixture into the lightest bin so far. Ties
        // break on name, so the same commit always plans the same split.
        foreach (var fixture in fixtures
                     .OrderByDescending(f => graph.TestCounts.GetValueOrDefault(f))
                     .ThenBy(f => f, StringComparer.Ordinal))
        {
            var target = 0;
            for (var i = 1; i < bins; i++)
                if (weights[i] < weights[target])
                    target = i;

            buckets[target].Add(fixture);
            weights[target] += graph.TestCounts.GetValueOrDefault(fixture);
        }

        return [..buckets.Select((b, i) => Shard.Of(i + 1, bins, graph, b))];
    }

    /// <summary>
    /// Writes the plan, or deletes any stale file when the plan cannot be trusted. The caller
    /// treats a missing file as "run unsharded", so a half-written plan must never survive.
    /// </summary>
    internal static void Write(string path, string scope, List<Shard> shards)
    {
        var covered = shards.SelectMany(s => s.Fixtures).ToList();

        // The whole point of generating the split is that it cannot drop or duplicate a
        // fixture. Assert it on the real data rather than trusting the loop above.
        var sound = shards.Count > 0
                    && shards.TrueForAll(s => s.Fixtures.Count > 0)
                    && covered.Count == covered.Distinct(StringComparer.Ordinal).Count();

        if (!sound)
        {
            Console.Error.WriteLine("note: shard plan is not a disjoint cover, writing none");
            File.Delete(path);
            return;
        }

        var plan = new JsonObject
        {
            ["scope"] = scope,
            ["total"] = shards.Sum(s => s.Tests),
            ["shards"] = new JsonArray([..shards.Select(s => (JsonNode)new JsonObject
            {
                ["index"] = s.Index,
                ["name"] = s.Name,
                ["filter"] = s.Filter,
                ["fixtures"] = string.Join(" ", s.Fixtures),
                ["tests"] = s.Tests,
            })]),
        };

        File.WriteAllText(path, plan.ToJsonString(new JsonSerializerOptions { WriteIndented = false }));

        foreach (var shard in shards)
            Console.Error.WriteLine($"shard {shard.Name}: {shard.Tests} tests in {shard.Fixtures.Count} "
                                    + $"fixture(s) — {string.Join(" ", shard.Fixtures)}");
    }
}

internal sealed record Shard(int Index, string Name, string Filter, List<string> Fixtures, int Tests)
{
    internal static Shard Of(int index, int of, ReachabilityGraph graph, IEnumerable<string> fixtures)
    {
        List<string> sorted = [..fixtures.Order(StringComparer.Ordinal)];

        // No Category=InWorld conjunct: the category sits on the class, so matching the class
        // already selects exactly the fixture's tests.
        var filter = string.Join("|", sorted.Select(f => $"FullyQualifiedName~{graph.FilterTokens[f]}"));

        return new Shard(index, $"{index}/{of}", filter, sorted,
            sorted.Sum(f => graph.TestCounts.GetValueOrDefault(f)));
    }
}
