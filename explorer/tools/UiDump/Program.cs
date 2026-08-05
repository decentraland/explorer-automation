using AltTester.AltTesterSDK.Driver;
using SkiaSharp;

namespace ExplorerAutomation.Tools.UiDump;

/// <summary>
/// Small CLI to inspect the live instrumented Explorer client through AltTester.
/// Subcommands: tree [namePattern], shot &lt;path.png&gt;, click &lt;name-or-id&gt;.
/// See README.md next to this file for usage.
/// </summary>
public static class Program
{
    // Beyond this many matches we skip the per-element component RPCs to keep dumps fast.
    private const int COMPONENT_FETCH_CAP = 400;
    private const int TEXT_FETCH_CAP = 150;

    private static readonly string[] KEY_COMPONENT_MARKERS =
    [
        "Button", "TMP_InputField", "Toggle", "TMP_Text", "TextMeshProUGUI",
        "InputField", "Slider", "Dropdown", "ScrollRect",
    ];

    public static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            PrintUsage();
            return 2;
        }

        AltDriver driver;
        try
        {
            driver = new AltDriver(
                host: "127.0.0.1",
                port: 13000,
                appName: "__default__",
                enableLogging: false,
                connectTimeout: 10);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to connect to AltTester at 127.0.0.1:13000: {ex.GetBaseException().Message}");
            return 1;
        }

        try
        {
            switch (args[0].ToLowerInvariant())
            {
                case "tree":
                    return RunTree(driver, args.Length > 1 ? args[1] : null, args.Contains("--all"));
                case "shot":
                    if (args.Length < 2)
                    {
                        Console.Error.WriteLine("shot requires an output path, e.g. UiDump shot /tmp/ui.png");
                        return 2;
                    }
                    return RunShot(driver, args[1]);
                case "click":
                    if (args.Length < 2)
                    {
                        Console.Error.WriteLine("click requires a name or numeric id, e.g. UiDump click SidebarMapButton");
                        return 2;
                    }
                    return RunClick(driver, args[1]);
                case "hoverclick":
                    if (args.Length < 3)
                    {
                        Console.Error.WriteLine("hoverclick requires <hover name-or-id> <click name-or-id> (single driver session).");
                        return 2;
                    }
                    return RunHoverClick(driver, args[1], args[2], useTap: false);
                case "hovertap":
                    if (args.Length < 3)
                    {
                        Console.Error.WriteLine("hovertap requires <hover name-or-id> <tap name-or-id> (single driver session).");
                        return 2;
                    }
                    return RunHoverClick(driver, args[1], args[2], useTap: true);
                case "dclick":
                    if (args.Length < 2)
                    {
                        Console.Error.WriteLine("dclick requires a name or numeric id.");
                        return 2;
                    }
                    return RunClick(driver, args[1], doubleClick: true);
                case "hover":
                    if (args.Length < 2)
                    {
                        Console.Error.WriteLine("hover requires a name or numeric id, e.g. UiDump hover OutfitSlot_1");
                        return 2;
                    }
                    return RunHover(driver, args[1]);
                case "settext":
                    if (args.Length < 3)
                    {
                        Console.Error.WriteLine("settext requires <name-or-id> <text>.");
                        return 2;
                    }
                    var textTarget = FindByNameOrId(driver, args[1]);
                    if (textTarget == null)
                        return 1;
                    textTarget.SetText(args[2], submit: true);
                    Console.WriteLine($"Set text '{args[2]}' on {textTarget.name} (id={textTarget.id}).");
                    return 0;
                case "key":
                    if (args.Length < 2)
                    {
                        Console.Error.WriteLine("key requires an AltKeyCode name, e.g. UiDump key I");
                        return 2;
                    }
                    return RunKey(driver, args[1]);
                case "sub":
                    if (args.Length < 2)
                    {
                        Console.Error.WriteLine("sub requires an AltTester path, e.g. UiDump sub \"//BackpackGrid/BackpackItem(Clone)[0]//*\"");
                        return 2;
                    }
                    return RunSub(driver, args[1], args.Contains("--all"));
                default:
                    PrintUsage();
                    return 2;
            }
        }
        finally
        {
            try { driver.Stop(); } catch { /* connection teardown is best-effort */ }
        }
    }

    private static void PrintUsage()
    {
        Console.Error.WriteLine("""
            UiDump — dump live Explorer UI via AltTester (127.0.0.1:13000)

            Usage:
              UiDump tree [namePattern] [--all]   list elements (name, id, parent path, key components)
                                                  namePattern filters case-insensitively; --all includes disabled objects
              UiDump shot <path.png>              save a screenshot of the live client
              UiDump click <name-or-id>           click an object by GameObject name (or numeric AltTester id)
              UiDump sub <altPath> [--all]        list objects matching an AltTester By.PATH query (cheap — no
                                                  full-scene enumeration); --all includes disabled objects
            """);
    }

    private static int RunTree(AltDriver driver, string namePattern, bool includeDisabled)
    {
        var all = driver.GetAllElements(enabled: !includeDisabled);
        var byTransformId = new Dictionary<int, AltObject>();
        foreach (var element in all)
            byTransformId.TryAdd(element.transformId, element);

        var matches = string.IsNullOrEmpty(namePattern)
            ? all
            : all.Where(e => e.name.Contains(namePattern, StringComparison.OrdinalIgnoreCase)).ToList();

        Console.Error.WriteLine($"{all.Count} elements total, {matches.Count} matched.");

        var fetchComponents = matches.Count <= COMPONENT_FETCH_CAP;
        if (!fetchComponents)
            Console.Error.WriteLine(
                $"More than {COMPONENT_FETCH_CAP} matches — skipping component lookup (narrow with a namePattern to get components).");
        var fetchText = matches.Count <= TEXT_FETCH_CAP;

        foreach (var element in matches)
        {
            var line = $"{element.name}  id={element.id}  path={BuildPath(element, byTransformId)}";
            if (!element.enabled)
                line += "  [disabled]";

            if (fetchComponents)
            {
                var components = SafeGetKeyComponents(element);
                if (components.Count > 0)
                    line += $"  comps={string.Join(",", components)}";

                if (fetchText && components.Any(c => c.Contains("Text") || c.Contains("InputField")))
                {
                    var text = SafeGetText(element);
                    if (text != null)
                        line += $"  text=\"{Truncate(text, 80)}\"";
                }
            }

            Console.WriteLine(line);
        }

        return 0;
    }

    private static string BuildPath(AltObject element, Dictionary<int, AltObject> byTransformId)
    {
        var parts = new List<string> { element.name };
        var guard = 0;
        var current = element;
        while (current.transformParentId != 0
               && byTransformId.TryGetValue(current.transformParentId, out var parent)
               && guard++ < 64)
        {
            parts.Add(parent.name);
            current = parent;
        }
        parts.Reverse();
        return "/" + string.Join("/", parts);
    }

    private static List<string> SafeGetKeyComponents(AltObject element)
    {
        try
        {
            return element.GetAllComponents()
                .Select(c => c.componentName)
                .Where(n => KEY_COMPONENT_MARKERS.Any(n.Contains))
                .Select(n => n[(n.LastIndexOf('.') + 1)..])
                .Distinct()
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    private static string SafeGetText(AltObject element)
    {
        try
        {
            var text = element.GetText();
            return string.IsNullOrWhiteSpace(text) ? null : text.ReplaceLineEndings(" ");
        }
        catch
        {
            return null;
        }
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max] + "…";

    private static int RunShot(AltDriver driver, string path)
    {
        // GetPNGScreenshot has a known StackOverflow bug in AltTester 2.3.x —
        // use GetScreenshot + SkiaSharp re-encode, same as Tests/Common/Snapshots/ScreenshotCapture.cs.
        var info = driver.GetScreenshot(size: default, screenShotQuality: 100);
        if (info.imageData == null || info.imageData.Length == 0)
        {
            Console.Error.WriteLine("AltTester returned an empty screenshot.");
            return 1;
        }

        using var data = SKData.CreateCopy(info.imageData);
        using var bmp = SKBitmap.Decode(data);
        if (bmp == null)
        {
            Console.Error.WriteLine($"Failed to decode AltTester screenshot ({info.imageData.Length} bytes).");
            return 1;
        }

        using var img = SKImage.FromBitmap(bmp);
        using var png = img.Encode(SKEncodedImageFormat.Png, 100);
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllBytes(fullPath, png.ToArray());
        Console.WriteLine($"Saved {bmp.Width}x{bmp.Height} screenshot to {fullPath}");
        return 0;
    }

    private static int RunHoverClick(AltDriver driver, string hoverNameOrId, string clickNameOrId, bool useTap)
    {
        var hoverTarget = FindByNameOrId(driver, hoverNameOrId);
        if (hoverTarget == null)
            return 1;

        hoverTarget.PointerEnter();
        Thread.Sleep(300);

        var clickTarget = FindByNameOrId(driver, clickNameOrId);
        if (clickTarget == null)
            return 1;

        if (useTap)
            clickTarget.Tap();
        else
            clickTarget.Click();
        Console.WriteLine(
            $"Hovered {hoverTarget.name} (id={hoverTarget.id}), {(useTap ? "tapped" : "clicked")} {clickTarget.name} (id={clickTarget.id}) at ({clickTarget.x},{clickTarget.y}).");
        return 0;
    }

    private static AltObject FindByNameOrId(AltDriver driver, string nameOrId)
    {
        if (nameOrId.StartsWith("//"))
        {
            try
            {
                return driver.FindObject(By.PATH, nameOrId);
            }
            catch (Exception pathEx)
            {
                Console.Error.WriteLine($"Could not find '{nameOrId}' by PATH ({pathEx.Message}).");
                return null;
            }
        }

        try
        {
            return driver.FindObject(By.NAME, nameOrId);
        }
        catch (Exception)
        {
            try
            {
                return driver.FindObject(By.ID, nameOrId);
            }
            catch (Exception idEx)
            {
                Console.Error.WriteLine($"Could not find '{nameOrId}' by NAME or ID ({idEx.Message}).");
                return null;
            }
        }
    }

    private static int RunHover(AltDriver driver, string nameOrId)
    {
        AltObject target;
        try
        {
            target = driver.FindObject(By.NAME, nameOrId);
        }
        catch (Exception)
        {
            try
            {
                target = driver.FindObject(By.ID, nameOrId);
            }
            catch (Exception idEx)
            {
                Console.Error.WriteLine($"Could not find object by NAME or ID ({idEx.Message}).");
                return 1;
            }
        }

        target.PointerEnter();
        Console.WriteLine($"PointerEnter on {target.name} (id={target.id}) at screen ({target.x},{target.y}).");
        return 0;
    }

    private static int RunKey(AltDriver driver, string keyName)
    {
        if (!Enum.TryParse<AltKeyCode>(keyName, ignoreCase: true, out var keyCode))
        {
            Console.Error.WriteLine($"Unknown AltKeyCode '{keyName}'.");
            return 2;
        }

        driver.PressKey(keyCode);
        Console.WriteLine($"Pressed {keyCode}.");
        return 0;
    }

    private static int RunSub(AltDriver driver, string altPath, bool includeDisabled)
    {
        // FindObjects with By.PATH is resolved app-side against the query only — unlike
        // `tree`, it does not enumerate the whole scene, so it is safe on heavy worlds.
        var matches = driver.FindObjects(By.PATH, altPath, enabled: !includeDisabled);
        Console.Error.WriteLine($"{matches.Count} matched for path {altPath}");

        var fetchComponents = matches.Count <= COMPONENT_FETCH_CAP;
        var fetchText = matches.Count <= TEXT_FETCH_CAP;
        foreach (var element in matches)
        {
            var line = $"{element.name}  id={element.id}";
            if (!element.enabled)
                line += "  [disabled]";

            if (fetchComponents)
            {
                var components = SafeGetKeyComponents(element);
                if (components.Count > 0)
                    line += $"  comps={string.Join(",", components)}";

                if (fetchText && components.Any(c => c.Contains("Text") || c.Contains("InputField")))
                {
                    var text = SafeGetText(element);
                    if (text != null)
                        line += $"  text=\"{Truncate(text, 80)}\"";
                }
            }

            Console.WriteLine(line);
        }

        return 0;
    }

    private static int RunClick(AltDriver driver, string nameOrId, bool doubleClick = false)
    {
        AltObject target;
        try
        {
            target = driver.FindObject(By.NAME, nameOrId);
        }
        catch (Exception nameEx)
        {
            try
            {
                target = driver.FindObject(By.ID, nameOrId);
            }
            catch (Exception idEx)
            {
                Console.Error.WriteLine($"Could not find object by NAME ({nameEx.Message}) or ID ({idEx.Message}).");
                return 1;
            }
        }

        target.Click();
        if (doubleClick)
        {
            // Two rapid clicks so Unity's IPointerClickHandler sees clickCount == 2
            // (BackpackItemView treats a double-click as Equip).
            Thread.Sleep(80);
            target.Click();
        }
        Console.WriteLine($"{(doubleClick ? "Double-clicked" : "Clicked")} {target.name} (id={target.id}) at screen ({target.x},{target.y}).");
        return 0;
    }
}
