using System.Text.Json;

namespace ExplorerAutomation.Tests.Common.Visual;

/// <summary>
/// Hot-reloads a built test scene into the running visual-host server. The host
/// server itself is started/stopped by metaforge (`mf explorer server start`);
/// this class only orchestrates the per-fixture bundle swap.
///
/// Per-fixture flow (called from <c>[OneTimeSetUp]</c>):
///   1. Read the test scene's <c>scene.json#main</c> to know its bundle path.
///   2. Copy that bundle and any sibling files into the host package's matching dir.
///   3. Mirror <c>assets/</c> if the scene has any.
///   4. Bump the bundle's mtime so chokidar always observes a change event, even when
///      the freshly-copied bytes happen to match what was already there.
///   5. Sleep just past chokidar's 800ms debounce so the reload fires before the
///      test starts polling for frame stability.
/// </summary>
public static class VisualHost
{
    private const string HOST_PACKAGE = "_host";

    public static string CurrentSceneId { get; private set; } = "";

    public static void Load(string sceneId)
    {
        if (string.IsNullOrWhiteSpace(sceneId))
            throw new ArgumentException("sceneId is required", nameof(sceneId));

        var scenesRoot = ResolveScenesRoot();
        var hostDir = Path.Combine(scenesRoot, "packages", HOST_PACKAGE);
        if (!Directory.Exists(hostDir))
            throw new DirectoryNotFoundException($"Host package not found: {hostDir}");

        var sourceDir = Path.Combine(scenesRoot, "packages", sceneId);
        if (!Directory.Exists(sourceDir))
            throw new DirectoryNotFoundException($"Scene package not found: {sourceDir}");

        // Bundle path comes from scene.json#main rather than a hardcoded bin/index.js
        // — keeps us honest if a scene ever customizes its main.
        var mainRelative = ReadMainFromSceneJson(sourceDir);
        var sourceBundle = Path.Combine(sourceDir, mainRelative);
        if (!File.Exists(sourceBundle))
            throw new FileNotFoundException(
                $"Scene was not built: {sourceBundle}. `mf explorer test` builds the workspace " +
                "automatically; if you're running dotnet test directly, `cd scenes && npm run build` first.");

        // Copy every file in the bundle's dir (covers index.js + siblings like index.js.map).
        var sourceBundleDir = Path.GetDirectoryName(sourceBundle)!;
        var hostBundleDir = Path.Combine(hostDir, Path.GetDirectoryName(mainRelative) ?? "bin");
        Directory.CreateDirectory(hostBundleDir);
        foreach (var file in Directory.EnumerateFiles(sourceBundleDir))
            File.Copy(file, Path.Combine(hostBundleDir, Path.GetFileName(file)), overwrite: true);

        // Force a mtime bump so chokidar always emits a change event, even when File.Copy
        // wrote bytes identical to the previous load (debounced events otherwise coalesce).
        var hostBundle = Path.Combine(hostDir, mainRelative);
        File.SetLastWriteTimeUtc(hostBundle, DateTime.UtcNow);

        // Mirror assets/ if present. Old assets aren't pruned — each scene's bundle only
        // references its own paths, so accumulation is harmless until disk pressure says otherwise.
        var sourceAssets = Path.Combine(sourceDir, "assets");
        if (Directory.Exists(sourceAssets))
            CopyDirectoryRecursive(sourceAssets, Path.Combine(hostDir, "assets"));

        CurrentSceneId = sceneId;
        Reporter.Log($"VisualHost loaded scene: {sceneId}");

        // chokidar debounces watch events for 800ms — wait past it so the websocket reload
        // fires before the test starts polling for frame stability.
        Thread.Sleep(900);
    }

    private static string ReadMainFromSceneJson(string sceneDir)
    {
        var path = Path.Combine(sceneDir, "scene.json");
        if (!File.Exists(path))
            throw new FileNotFoundException($"scene.json not found in {sceneDir}");

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        if (!doc.RootElement.TryGetProperty("main", out var main) || main.ValueKind != JsonValueKind.String)
            throw new InvalidOperationException($"scene.json in {sceneDir} has no `main` field.");

        return main.GetString()!;
    }

    private static void CopyDirectoryRecursive(string source, string dest)
    {
        Directory.CreateDirectory(dest);
        foreach (var file in Directory.EnumerateFiles(source))
            File.Copy(file, Path.Combine(dest, Path.GetFileName(file)), overwrite: true);
        foreach (var dir in Directory.EnumerateDirectories(source))
            CopyDirectoryRecursive(dir, Path.Combine(dest, Path.GetFileName(dir)));
    }

    private static string ResolveScenesRoot()
    {
        // Walk upward from the test assembly until we find scenes/package.json.
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 8 && dir != null; i++)
        {
            var candidate = Path.Combine(dir, "scenes");
            if (File.Exists(Path.Combine(candidate, "package.json"))) return candidate;
            dir = Path.GetDirectoryName(dir);
        }
        throw new DirectoryNotFoundException(
            "Could not locate the scenes/ workspace. Run from the explorer-automation repo.");
    }
}
