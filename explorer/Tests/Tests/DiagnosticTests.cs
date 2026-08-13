namespace ExplorerAutomation.Tests.Tests;

/// <summary>
/// On-demand, scene-discovery diagnostics — NOT part of any CI category beyond
/// <c>Diagnostic</c>. These exist to learn GameObject names / IDs while writing
/// or repairing views; delete the relevant test once its locators are pinned.
///
/// Both run against whatever state the connected Explorer is currently in
/// (neither inherits <see cref="BaseTest"/>, so no <c>EnsureInWorld</c> navigation
/// moves the scene out from under the dump). Each is invoked individually:
///   • dotnet test Tests/ --filter "DumpVisibleObjects"             (auth/verification object tree)
///   • dotnet test Tests/ --filter "DiagnoseAuthScreenUsernameLabel" (cached-account username label)
/// </summary>
[TestFixture]
[Category("Diagnostic")]
[Order(14)]
public class DiagnosticTests
{
    private AltDriver Driver => CommonStuff.AltDriver;

    /// <summary>
    /// Dumps the auth/verification-related GameObjects currently visible to
    /// AltTester, then walks the descendant tree of
    /// <c>Authentication.MainScreen(Clone)</c> (where the verification sub-screens
    /// live). Use to confirm locator names for the cross-stack wallet/verification
    /// flows — run while the Explorer sits on the relevant screen (e.g. the
    /// post-Metamask verification screen).
    ///
    /// (Formerly the standalone <c>SceneInspector</c> fixture; folded in here so
    /// the on-demand scene-dump diagnostics live in one place and share the
    /// <see cref="FilterByName"/> helper.)
    /// </summary>
    [Test]
    public void DumpVisibleObjects()
    {
        Assert.That(Driver, Is.Not.Null, "AltDriver must already be connected");

        var allObjects = Driver.GetAllElements();
        Reporter.Log($"Total scene elements visible to AltTester: {allObjects.Count}");

        // Filter to anything that looks auth/verification-related.
        var interesting = FilterByName(allObjects,
            "Verif", "Auth", "Code", "Sign", "Metamask", "Dapp", "Wallet", ".Screen");

        Reporter.Log($"Auth/Verification-related objects ({interesting.Count}):");
        foreach (var obj in interesting)
        {
            Reporter.Log($"  name='{obj.name}' enabled={obj.enabled} id={obj.id} parentId={obj.transformParentId}");
        }

        // Walk the descendant tree of Authentication.MainScreen(Clone) — that's
        // where verification sub-screens live. Recurse 4 levels to cover the
        // nested screen → container → element chain.
        var authScreen = allObjects.Find(o => o.name == "Authentication.MainScreen(Clone)");
        if (authScreen != null)
        {
            Reporter.Log($"--- Descendant tree of Authentication.MainScreen(Clone) (transformId={authScreen.transformId}) ---");
            DumpDescendants(allObjects, authScreen.transformId, depth: 0, maxDepth: 4);
        }
        else
        {
            Reporter.Log("Authentication.MainScreen(Clone) NOT in scene!");
        }
    }

    private static void DumpDescendants(List<AltObject> all, int parentId, int depth, int maxDepth)
    {
        if (depth >= maxDepth) return;
        var indent = new string(' ', depth * 2);
        foreach (var c in all.FindAll(o => o.transformParentId == parentId))
        {
            Reporter.Log($"{indent}name='{c.name}' enabled={c.enabled} transformId={c.transformId}");
            DumpDescendants(all, c.transformId, depth + 1, maxDepth);
        }
    }

    /// <summary>
    /// All elements whose name contains any of <paramref name="needles"/>
    /// (case-insensitive). Shared by the scene-dump diagnostics: filtering by a
    /// cheap name match first keeps the per-element <c>GetText</c> round-trip
    /// count low (each round-trips to the running app).
    /// </summary>
    private static List<AltObject> FilterByName(List<AltObject> all, params string[] needles) =>
        all.FindAll(o => needles.Any(n => o.name?.Contains(n, StringComparison.OrdinalIgnoreCase) ?? false));

    /// <summary>
    /// Locator-discovery utility for the cached-account "Welcome &lt;name&gt;" label on
    /// <c>Authentication.MainScreen(Clone)</c>. Used to fill in the placeholder
    /// path in <c>AuthenticationMainScreenView.UsernameLabel</c> referenced by
    /// <c>CrossWebFirstLoginTests.WebFirstLoginUsernameAssert</c>.
    ///
    /// Prerequisites:
    ///   • AltTester Desktop on port 13000.
    ///   • An instrumented Explorer connected, sitting on the cached-account
    ///     screen (i.e. a logged-in account is saved locally — the
    ///     JumpIntoWorld + UseAnotherAccount buttons are visible).
    ///
    /// Run with:
    ///   dotnet test explorer/Tests/ --filter "DiagnoseAuthScreenUsernameLabel" \
    ///     --logger "console;verbosity=normal"
    ///
    /// Reads every descendant text element under Authentication.MainScreen(Clone)
    /// and prints its name, id, and current displayed text. The username label
    /// is the one whose text is the player's actual display name (alphanumeric,
    /// not a static UI string like "JUMP INTO DECENTRALAND" or "WELCOME BACK").
    /// Copy its name (preferred) or build a <c>By.PATH</c> from the dump.
    /// </summary>
    [Test]
    public void DiagnoseAuthScreenUsernameLabel()
    {
        // Report the current screen state without bailing — the diagnostic is
        // useful in any state because we're trying to learn GameObject naming
        // conventions, not just the cached-account locator.
        Console.WriteLine("=== Screen state probe ===");
        ProbeNamed("Authentication.MainScreen(Clone)");
        ProbeNamed("LoginSelection.Screen");
        ProbeNamed("JumpIntoWorldButton");
        ProbeNamed("UseAnotherAccountButton");
        ProbeNamed("Verification.Dapp.Screen");
        ProbeNamed("OtpVerification.Screen");
        ProbeNamed("Lobby.NewAccount.Screen");
        ProbeNamed("SidebarView");

        Console.WriteLine();
        DumpTextElementsFromScene();
    }

    private void ProbeNamed(string name)
    {
        try
        {
            var obj = Driver.FindObject(By.NAME, name);
            Console.WriteLine($"[present] {name} id={obj.id} transformId={obj.transformId}");
        }
        catch
        {
            Console.WriteLine($"[absent ] {name}");
        }
    }

    private void DumpTextElementsFromScene()
    {
        Console.WriteLine("\n=== Filtered scene dump — candidates for username label ===");
        Console.WriteLine("Heuristic: names matching welcome/username/name/displayname/profile/account/title.");
        Console.WriteLine("Per-element GetText is slow (round-trip each), so we filter first.\n");
        try
        {
            var all = Driver.GetAllElements();
            Console.WriteLine($"Total elements in scene: {all.Count}");

            // Build id → element map for O(1) parent lookups. Both `id` and
            // `transformId` may be used as the "key" depending on which the
            // parent points at — index by both to be safe.
            var byId = new Dictionary<int, AltObject>();
            foreach (var el in all)
            {
                byId[el.id] = el;
                byId[el.transformId] = el;
            }

            // Aggressively filter by name to keep round-trip count under ~50.
            var candidates = FilterByName(all, "welcome", "username", "displayname", "userdata",
                                               "profile", "account", "title", "name");
            Console.WriteLine($"Name-match candidates: {candidates.Count}");

            int dumped = 0;
            foreach (var el in candidates)
            {
                string text;
                try
                {
                    var node = Driver.FindObject(By.ID, el.id.ToString());
                    text = node.GetText();
                }
                catch
                {
                    text = null;
                }
                if (string.IsNullOrWhiteSpace(text)) continue;
                var t = text.Trim();
                if (t.Length > 64 || t.Contains("\n") || t.StartsWith("http")) continue;
                dumped++;
                Console.WriteLine($"  name={el.name} id={el.id} parentId={el.transformParentId} text='{t}'");

                // Walk up the parent chain via the in-memory map (no extra
                // round-trips). Stops at root or after 8 levels.
                var ancestors = new List<string>();
                var pid = el.transformParentId;
                for (int i = 0; i < 8 && pid != 0; i++)
                {
                    if (!byId.TryGetValue(pid, out var parent)) break;
                    ancestors.Add(parent.name);
                    pid = parent.transformParentId;
                }
                Console.WriteLine($"      ancestors: {string.Join(" → ", ancestors)}");
            }
            Console.WriteLine($"\n=== {dumped} candidate elements with non-empty text ===");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GetAllElements scan failed: {ex.Message}");
        }
    }
}
