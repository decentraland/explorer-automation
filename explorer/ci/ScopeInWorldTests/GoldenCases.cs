namespace ExplorerAutomation.Ci.ScopeInWorldTests;

/// <summary>
/// Golden expectations for the closure, run with <c>--self-test</c>. Each expectation was
/// derived by reading the fixtures, not by recording what the tool printed.
/// </summary>
internal static class GoldenCases
{
    /// <summary>Stands in for "every InWorld fixture", so adding a fixture does not edit the table.</summary>
    private const string EveryFixture = "<all>";

    private static readonly (string Changed, string[] Expected)[] Cases =
    [
        // Element primitives and the fixture base class are behind every fixture.
        ("explorer/Tests/Views/Elements/Locatable.cs", [EveryFixture]),
        ("explorer/Tests/Tests/BaseTest.cs", [EveryFixture]),

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

    public static int Run(ReachabilityGraph graph)
    {
        Console.WriteLine($"InWorld fixtures ({graph.Fixtures.Count}): {string.Join(" ", graph.Fixtures)}");
        Console.WriteLine();

        var failed = 0;
        foreach (var (changed, expected) in Cases)
        {
            var want = expected is [EveryFixture] ? graph.Fixtures : [..expected];
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

        // Stated separately from the set above so the canary survives a fixture rename.
        var places = Program.Resolve(graph, ["explorer/Tests/Views/ExplorePanelSections/ExplorePanelPlacesView.cs"]);
        var leaked = places.Fixtures.Where(f => f.StartsWith("Backpack", StringComparison.Ordinal)).ToList();
        if (leaked.Count > 0)
        {
            failed++;
            Console.WriteLine($"FAIL  ExplorePanelPlacesView.cs leaked into [{string.Join(" ", leaked)}] — the graph collapsed");
        }
        else
        {
            Console.WriteLine("PASS  ExplorePanelPlacesView.cs reaches no Backpack fixture");
        }

        Console.WriteLine();
        Console.WriteLine(failed == 0 ? "all golden cases passed" : $"{failed} golden case(s) failed");
        return failed == 0 ? 0 : 1;
    }

    private static string Fallback(ScopeResult result) =>
        result.FailSafeReason is { } reason ? $"  (fail-safe: {reason})" : string.Empty;
}
