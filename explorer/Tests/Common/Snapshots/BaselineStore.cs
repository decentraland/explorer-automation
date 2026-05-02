namespace ExplorerAutomation.Tests.Common.Snapshots;

internal static class BaselineStore
{
    private const string PROJECT_FILE = "Tests.csproj";
    private const string BASELINES_DIR = "Baselines";

    public static string ResolvePath(string snapshotName)
    {
        var ctx = TestContext.CurrentContext;
        var className = ctx.Test.ClassName ?? "Unknown";
        var shortClass = className.Substring(className.LastIndexOf('.') + 1);
        var methodName = ctx.Test.MethodName ?? "Unknown";

        var fileName = $"{methodName}__{Sanitize(snapshotName)}.png";
        return Path.Combine(FindProjectRoot(), BASELINES_DIR, shortClass, fileName);
    }

    public static bool Exists(string path) => File.Exists(path);

    public static byte[] Read(string path) => File.ReadAllBytes(path);

    public static void Write(string path, byte[] pngBytes)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllBytes(path, pngBytes);
    }

    private static string FindProjectRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (dir.GetFiles(PROJECT_FILE).Length > 0)
                return dir.FullName;
            dir = dir.Parent;
        }
        return Directory.GetCurrentDirectory();
    }

    private static string Sanitize(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "default";
        var invalid = Path.GetInvalidFileNameChars();
        var span = name.AsSpan();
        Span<char> buf = stackalloc char[span.Length];
        for (var i = 0; i < span.Length; i++)
            buf[i] = Array.IndexOf(invalid, span[i]) >= 0 ? '_' : span[i];
        return new string(buf);
    }
}
