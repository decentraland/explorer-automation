using System.Runtime.CompilerServices;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace ExplorerAutomation.Ci.ScopeInWorldTests;

/// <summary>
/// Resolves which <c>[Category("InWorld")]</c> fixtures a set of changed files can reach, and
/// splits a resolved scope across runners. Reads repo-relative changed paths from stdin (or takes
/// an already-resolved <c>--scope</c>), prints <c>ALL</c>, <c>NONE</c> or <c>FIXTURES: A B C</c> to
/// stdout, the audit trail to stderr, and any <c>--shard-plan</c> to that path.
/// </summary>
internal static class Program
{
    private const string TestProject = "explorer/Tests/Tests.csproj";

    private static async Task<int> Main(string[] args)
    {
        try
        {
            // Must happen before any MSBuild type is touched, hence the separate method below.
            if (!MSBuildLocator.IsRegistered)
                MSBuildLocator.RegisterDefaults();

            return await RunAsync(args);
        }
        catch (Exception ex)
        {
            return FailSafe(ex.ToString());
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task<int> RunAsync(string[] args)
    {
        var selfTest = args.Contains("--self-test");
        var repoRoot = Option(args, "--repo-root") ?? FindRepoRoot();
        var projectPath = Option(args, "--project") ?? Path.Combine(repoRoot, TestProject);

        if (!File.Exists(projectPath))
            return FailSafe($"test project not found at {projectPath}");

        using var workspace = MSBuildWorkspace.Create();
        var failures = new List<string>();
        workspace.RegisterWorkspaceFailedHandler(e => failures.Add(e.Diagnostic.Message));

        var project = await workspace.OpenProjectAsync(projectPath);
        var compilation = await project.GetCompilationAsync();
        if (compilation is null)
            return FailSafe($"no compilation for {projectPath}");

        // Unresolved references make every symbol lookup silently return null, which would
        // read as "nothing is affected" instead of an error.
        var errors = compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Take(10)
            .ToList();
        if (errors.Count > 0)
            return FailSafe($"compilation has errors, symbols cannot be trusted:{Environment.NewLine}"
                            + string.Join(Environment.NewLine, errors.Select(e => "  " + e)));

        foreach (var failure in failures)
            Console.Error.WriteLine($"note: MSBuild reported: {failure}");

        var graph = ReachabilityGraph.Build(compilation, repoRoot);
        if (graph.Fixtures.Count == 0)
            return FailSafe("found no [Category(\"InWorld\")] fixtures");

        if (graph.Untrusted is { } untrusted)
        {
            if (!selfTest)
                return FailSafe(untrusted);

            Console.WriteLine($"FAIL  graph is untrusted: {untrusted}");
            return 1;
        }

        if (selfTest)
            return GoldenCases.Run(graph);

        var planPath = Option(args, "--shard-plan");

        // Every exit path but a written plan must leave none behind: the caller reads a missing
        // plan as "run the whole selection on one runner".
        if (planPath is not null)
            File.Delete(planPath);

        List<string> selected;

        // A scope already resolved elsewhere, handed back in the vocabulary this tool prints,
        // so nothing has to encode the same answer twice.
        if (Option(args, "--scope") is { } requested)
        {
            if (requested.Trim() == "NONE")
            {
                Console.Error.WriteLine("=> NONE: requested explicitly, nothing to plan.");
                Console.WriteLine("NONE");
                return 0;
            }

            if (!TrySelect(graph, requested, out selected, out var why))
                return FailSafe(why);

            Console.Error.WriteLine($"=> requested explicitly: {string.Join(" ", selected)}");
        }
        else
        {
            var result = Resolve(graph, ReadChangedFiles());

            foreach (var line in result.Trail)
                Console.Error.WriteLine(line);

            if (result.FailSafeReason is { } reason)
                return FailSafe(reason);

            if (result.Fixtures.Count == 0)
            {
                Console.Error.WriteLine("=> NONE: no InWorld fixture reaches any changed file.");
                Console.WriteLine("NONE");
                return 0;
            }

            selected = result.Fixtures;
            Console.Error.WriteLine(selected.Count == graph.Fixtures.Count
                ? $"=> ALL: the closure covers every InWorld fixture ({string.Join(" ", graph.Fixtures)})."
                : $"=> FIXTURES: {string.Join(" ", selected)}");
        }

        var scope = selected.Count == graph.Fixtures.Count
            ? "ALL"
            : $"FIXTURES: {string.Join(" ", selected)}";
        Console.WriteLine(scope);

        if (planPath is not null)
        {
            if (graph.CountsUntrusted is { } untrustedCounts)
                Console.Error.WriteLine($"note: test counts are untrusted ({untrustedCounts}), planning one shard");

            var bins = int.TryParse(Option(args, "--shards"), out var requestedBins)
                ? requestedBins
                : Shards.DefaultCount;

            Shards.Write(planPath, scope, Shards.Plan(graph, selected, bins));
        }

        return 0;
    }

    /// <summary>
    /// Reads a scope back out of the string form this tool prints — <c>ALL</c> or
    /// <c>FIXTURES: A B</c>. Every name must be a fixture of this graph, or the scope was
    /// resolved against different code and cannot be planned for.
    /// </summary>
    internal static bool TrySelect(
        ReachabilityGraph graph, string requested, out List<string> selected, out string why)
    {
        const string prefix = "FIXTURES:";

        selected = [];
        var scope = requested.Trim();

        if (scope == "ALL")
        {
            selected = graph.Fixtures;
            why = string.Empty;
            return true;
        }

        if (!scope.StartsWith(prefix, StringComparison.Ordinal))
        {
            why = $"--scope '{requested}' is neither ALL nor '{prefix} <names>'";
            return false;
        }

        var named = scope[prefix.Length..]
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (named.Length == 0)
        {
            why = $"--scope '{requested}' names no fixture";
            return false;
        }

        if (Array.Find(named, n => !graph.Fixtures.Contains(n, StringComparer.Ordinal)) is { } unknown)
        {
            why = $"--scope names {unknown}, which is not an InWorld fixture of this project";
            return false;
        }

        selected = [..named.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
        why = string.Empty;
        return true;
    }

    internal static ScopeResult Resolve(ReachabilityGraph graph, IEnumerable<string> changed)
    {
        var selected = new SortedSet<string>(StringComparer.Ordinal);
        var trail = new List<string>();

        foreach (var file in changed)
        {
            if (!file.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                || !file.StartsWith("explorer/Tests/", StringComparison.OrdinalIgnoreCase))
            {
                trail.Add($"{file}: not a compiled test source, ignored");
                continue;
            }

            if (!graph.Files.Contains(file))
                return new ScopeResult(
                    $"{file} is not a document of {TestProject} (added, deleted or renamed?)", [], trail);

            var hits = graph.Fixtures.Where(f => graph.Reach[f].ContainsKey(file)).ToList();
            if (hits.Count == 0)
            {
                trail.Add($"{file}: no InWorld fixture reaches it");
                continue;
            }

            foreach (var fixture in hits)
            {
                selected.Add(fixture);
                trail.Add($"{file} -> {fixture} (via {graph.Reach[fixture][file]})");
            }
        }

        return new ScopeResult(null, [..selected], trail);
    }

    private static List<string> ReadChangedFiles()
    {
        var files = new List<string>();
        while (Console.In.ReadLine() is { } line)
        {
            var path = line.Trim().Replace('\\', '/');
            if (path.Length > 0)
                files.Add(path);
        }

        return files;
    }

    private static int FailSafe(string reason)
    {
        Console.Error.WriteLine($"=> ALL (fail-safe): {reason}");
        Console.WriteLine("ALL");
        return 0;
    }

    private static string? Option(string[] args, string name)
    {
        var i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    private static string FindRepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (Directory.Exists(Path.Combine(dir.FullName, ".git"))
                || File.Exists(Path.Combine(dir.FullName, ".git")))
                return dir.FullName;

        return Directory.GetCurrentDirectory();
    }
}

internal sealed record ScopeResult(string? FailSafeReason, List<string> Fixtures, List<string> Trail);
