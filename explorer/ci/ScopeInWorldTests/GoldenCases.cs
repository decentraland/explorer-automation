namespace ExplorerAutomation.Ci.ScopeInWorldTests;

/// <summary>
/// Golden expectations for the closure, run with <c>--self-test</c>. Each expectation was
/// derived by reading the fixtures, not by recording what the tool printed.
/// </summary>
internal static class GoldenCases
{
    /// <summary>Stands in for "every InWorld fixture", so adding a fixture does not edit the table.</summary>
    private const string EveryFixture = "<all>";

    /// <summary>A fixture quietly leaving the category must read as drift, not as narrower scope.</summary>
    private const int ExpectedFixtureCount = 16;

    private static readonly (string Changed, string[] Expected)[] Cases =
    [
        // Element primitives and the fixture base class are behind every fixture.
        ("explorer/Tests/Views/Elements/Locatable.cs", [EveryFixture]),
        ("explorer/Tests/Tests/BaseTest.cs", [EveryFixture]),

        // Boot views: driven only from BaseTest.EnsureInWorld, which NUnit invokes by
        // reflection. These are the cases a type-level base edge alone would miss.
        ("explorer/Tests/Views/SplashView.cs", [EveryFixture]),
        ("explorer/Tests/Views/LoadingScreenView.cs", [EveryFixture]),
        ("explorer/Tests/Views/MinimumSpecsScreenView.cs", [EveryFixture]),

        // Only the two fixtures that open the Settings section.
        ("explorer/Tests/Views/ExplorePanelSections/ExplorePanelSettingsView.cs",
            ["ExplorePanelTests", "ShortcutsTests"]),

        // Auth-only screen; the OTP fixtures are [Order(1000+)] and not in the category.
        ("explorer/Tests/Views/OtpVerificationScreenView.cs", []),

        ("explorer/Tests/Views/ExplorePanelSections/ExplorePanelBackpackView.cs",
        [
            "BackpackEmotesTests", "BackpackOutfitsTests", "BackpackWearablesTests",
            "ExplorePanelTests", "ShortcutsTests",
        ]),

        // Anti-collapse canary: a sibling section of Backpack under the same aggregate view.
        ("explorer/Tests/Views/ExplorePanelSections/ExplorePanelPlacesView.cs",
            ["ExplorePanelTests", "PlacesTests", "ShortcutsTests"]),
    ];

    /// <summary>
    /// The only views no InWorld fixture drives. Asserted as an exact set: a new unreachable
    /// view is far more likely to be a hole in the closure than a genuinely unused page object.
    /// </summary>
    private static readonly string[] UnreachableViews =
    [
        // Driven only from the [Category("Auth")] OTP fixtures and LoggedOutAuthBaseTest.
        "explorer/Tests/Views/OtpVerificationScreenView.cs",
        "explorer/Tests/Views/WelcomeNewAccountScreenView.cs",
    ];

    public static int Run(ReachabilityGraph graph)
    {
        Console.WriteLine($"InWorld fixtures ({graph.Fixtures.Count}): {string.Join(" ", graph.Fixtures)}");
        Console.WriteLine();

        var failed = 0;
        foreach (var (changed, expected) in Cases)
        {
            var want = expected is [EveryFixture] ? [..graph.Fixtures] : Sorted(expected);
            var result = Program.Resolve(graph, [changed]);
            var got = result.Fixtures;

            var ok = result.FailSafeReason is null && got.SequenceEqual(want, StringComparer.Ordinal);
            if (!ok)
                failed++;

            Console.WriteLine($"{(ok ? "PASS" : "FAIL")}  {changed}");
            Console.WriteLine($"        got      [{string.Join(" ", got)}]{Fallback(result)}");
            if (!ok)
                Console.WriteLine($"        expected [{string.Join(" ", want)}]");
        }

        failed += Check($"fixture count is {ExpectedFixtureCount}",
            graph.Fixtures.Count == ExpectedFixtureCount,
            $"found {graph.Fixtures.Count}");

        // Stated separately from the Places case above so the canary survives a fixture rename.
        var places = Program.Resolve(graph, ["explorer/Tests/Views/ExplorePanelSections/ExplorePanelPlacesView.cs"]);
        var leaked = places.Fixtures.Where(f => f.StartsWith("Backpack", StringComparison.Ordinal)).ToList();
        failed += Check("ExplorePanelPlacesView.cs reaches no Backpack fixture",
            leaked.Count == 0,
            $"leaked into [{string.Join(" ", leaked)}] — the graph collapsed");

        var unreachable = UnreachableViewsIn(graph);
        failed += Check("unreachable views match the allow-list",
            unreachable.SequenceEqual(Sorted(UnreachableViews), StringComparer.Ordinal),
            $"got{Environment.NewLine}          {string.Join(Environment.NewLine + "          ", unreachable)}"
            + $"{Environment.NewLine}        expected{Environment.NewLine}          "
            + string.Join(Environment.NewLine + "          ", Sorted(UnreachableViews)));

        failed += CheckShards(graph);

        Console.WriteLine();
        Console.WriteLine(failed == 0 ? "all golden cases passed" : $"{failed} golden case(s) failed");
        return failed == 0 ? 0 : 1;
    }

    /// <summary>
    /// The split is generated instead of hand-written precisely so it cannot drop a fixture, so
    /// that is what gets asserted — no total is pinned, or every new test would fail this.
    /// </summary>
    private static int CheckShards(ReachabilityGraph graph)
    {
        var failed = 0;
        var declared = graph.Fixtures.Sum(f => graph.TestCounts.GetValueOrDefault(f));

        failed += Check("test counts are trusted",
            graph.CountsUntrusted is null, graph.CountsUntrusted ?? string.Empty);

        var uncounted = graph.Fixtures.Where(f => graph.TestCounts.GetValueOrDefault(f) == 0).ToList();
        failed += Check("every fixture counts at least one test",
            uncounted.Count == 0, $"[{string.Join(" ", uncounted)}] counted none");

        // A token that is a substring of another puts one fixture's tests on both shards.
        var tokens = graph.Fixtures.ConvertAll(f => graph.FilterTokens[f]);
        var overlapping = tokens.Where(t => tokens.Count(o => o.Contains(t, StringComparison.Ordinal)) > 1).ToList();
        failed += Check("no filter token contains another",
            overlapping.Count == 0, $"[{string.Join(" ", overlapping)}]");

        // Tied to the constant CI plans with, so that count is always one of the covers asserted.
        foreach (var bins in new[] { 1, Shards.DefaultCount, Shards.DefaultCount + 1 })
        {
            var plan = Shards.Plan(graph, graph.Fixtures, bins);
            var covered = Sorted(plan.SelectMany(s => s.Fixtures));

            failed += Check($"{bins} shard(s) cover every fixture exactly once",
                covered.SequenceEqual(graph.Fixtures, StringComparer.Ordinal)
                && plan.TrueForAll(s => s.Fixtures.Count > 0),
                $"got [{string.Join(" ", covered)}]");

            var carried = plan.Sum(s => s.Tests);
            failed += Check($"{bins} shard(s) carry every test",
                carried == declared, $"shards carry {carried}, fixtures declare {declared}");

            Console.WriteLine($"        {string.Join(" | ", plan.Select(s => $"{s.Name} {s.Tests}t/{s.Fixtures.Count}f"))}");
        }

        // Two bins over one fixture must not invent a second, empty shard — its filter would
        // match nothing and vstest treats that as a failure, not as an empty pass.
        var single = Shards.Plan(graph, [graph.Fixtures[0]], 2);
        failed += Check("a one-fixture scope plans one shard", single.Count == 1, $"planned {single.Count}");

        // A scope too small to earn a second runner. Accumulated from the lightest fixtures
        // rather than named, so a rename or a changed count cannot quietly void the case.
        var light = graph.Fixtures.OrderBy(f => graph.TestCounts.GetValueOrDefault(f))
            .ThenBy(f => f, StringComparer.Ordinal).ToList();

        var small = new List<string>();
        foreach (var fixture in light)
        {
            if (Weight(graph, small) + graph.TestCounts.GetValueOrDefault(fixture) > Shards.MinTestsToSplit)
                break;
            small.Add(fixture);
        }

        var big = new List<string>(small);
        foreach (var fixture in light.Where(f => !small.Contains(f, StringComparer.Ordinal)))
        {
            big.Add(fixture);
            if (Weight(graph, big) > Shards.MinTestsToSplit)
                break;
        }

        failed += Check($"{Weight(graph, small)} tests over {small.Count} fixtures plan one shard",
            small.Count > 1 && Shards.Plan(graph, small, 2).Count == 1,
            $"planned {Shards.Plan(graph, small, 2).Count} from [{string.Join(" ", small)}]");

        failed += Check($"{Weight(graph, big)} tests over {big.Count} fixtures plan two",
            Shards.Plan(graph, big, 2).Count == 2,
            $"planned {Shards.Plan(graph, big, 2).Count} from [{string.Join(" ", big)}]");

        return failed + CheckScopeRoundTrip(graph);
    }

    /// <summary>
    /// The split is planned from the scope string a PR run resolved, so that string has to read
    /// back as the same fixtures — and a name this project does not have must refuse rather than
    /// plan a shard whose filter matches nothing.
    /// </summary>
    private static int CheckScopeRoundTrip(ReachabilityGraph graph)
    {
        var failed = 0;
        var pair = Sorted([graph.Fixtures[0], graph.Fixtures[^1]]);

        failed += Check("ALL reads back as every fixture",
            Program.TrySelect(graph, "ALL", out var all, out _)
            && all.SequenceEqual(graph.Fixtures, StringComparer.Ordinal),
            $"got [{string.Join(" ", all)}]");

        failed += Check("a resolved scope reads back as the fixtures it names",
            Program.TrySelect(graph, $"FIXTURES: {string.Join(" ", pair)}", out var back, out _)
            && back.SequenceEqual(pair, StringComparer.Ordinal),
            $"got [{string.Join(" ", back)}] for [{string.Join(" ", pair)}]");

        failed += Check("a fixture this project does not have is refused",
            !Program.TrySelect(graph, "FIXTURES: NotAFixtureTests", out _, out _), "it was accepted");

        return failed;
    }

    private static List<string> UnreachableViewsIn(ReachabilityGraph graph) =>
        graph.Files
            .Where(f => f.StartsWith("explorer/Tests/Views/", StringComparison.OrdinalIgnoreCase)
                        && f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                        && graph.Fixtures.TrueForAll(fixture => !graph.Reach[fixture].ContainsKey(f)))
            .Order(StringComparer.Ordinal)
            .ToList();

    private static int Check(string what, bool ok, string detail)
    {
        Console.WriteLine($"{(ok ? "PASS" : "FAIL")}  {what}");
        if (ok)
            return 0;

        Console.WriteLine($"        {detail}");
        return 1;
    }

    private static List<string> Sorted(IEnumerable<string> values) => [..values.Order(StringComparer.Ordinal)];

    private static int Weight(ReachabilityGraph graph, IEnumerable<string> fixtures) =>
        fixtures.Sum(f => graph.TestCounts.GetValueOrDefault(f));

    private static string Fallback(ScopeResult result) =>
        result.FailSafeReason is { } reason ? $"  (fail-safe: {reason})" : string.Empty;
}
