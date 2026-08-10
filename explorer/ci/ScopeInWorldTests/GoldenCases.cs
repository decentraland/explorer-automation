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

        Console.WriteLine();
        Console.WriteLine(failed == 0 ? "all golden cases passed" : $"{failed} golden case(s) failed");
        return failed == 0 ? 0 : 1;
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

    private static string Fallback(ScopeResult result) =>
        result.FailSafeReason is { } reason ? $"  (fail-safe: {reason})" : string.Empty;
}
