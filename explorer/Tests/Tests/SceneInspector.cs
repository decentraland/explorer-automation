namespace ExplorerAutomation.Tests.Tests;

/// <summary>
/// Diagnostic-only fixture. Dumps the currently-visible GameObjects in the
/// Unity scene to help confirm locator names. Not part of any CI category;
/// only run on demand via `dotnet test --filter Name=DumpVisibleObjects`.
///
/// Does NOT inherit from BaseTest so it skips the EnsureInWorld lifecycle —
/// the whole point is to inspect the scene in whatever state it's currently
/// in (e.g. post-Metamask verification screen) without the base class
/// navigating away.
/// </summary>
[TestFixture]
[Category("Diagnostic")]
public class SceneInspector
{
    [Test]
    public void DumpVisibleObjects()
    {
        var altDriver = CommonStuff.AltDriver;
        Assert.That(altDriver, Is.Not.Null, "AltDriver must already be connected");

        var allObjects = altDriver.GetAllElements();
        Reporter.Log($"Total scene elements visible to AltTester: {allObjects.Count}");

        // Filter to anything that looks auth/verification-related.
        var interesting = allObjects.FindAll(o =>
            o.name.Contains("Verif", StringComparison.OrdinalIgnoreCase) ||
            o.name.Contains("Auth", StringComparison.OrdinalIgnoreCase) ||
            o.name.Contains("Code", StringComparison.OrdinalIgnoreCase) ||
            o.name.Contains("Sign", StringComparison.OrdinalIgnoreCase) ||
            o.name.Contains("Metamask", StringComparison.OrdinalIgnoreCase) ||
            o.name.Contains("Dapp", StringComparison.OrdinalIgnoreCase) ||
            o.name.Contains("Wallet", StringComparison.OrdinalIgnoreCase) ||
            o.name.Contains(".Screen", StringComparison.OrdinalIgnoreCase));

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

    private static void DumpDescendants(System.Collections.Generic.List<AltObject> all, int parentId, int depth, int maxDepth)
    {
        if (depth >= maxDepth) return;
        var indent = new string(' ', depth * 2);
        foreach (var c in all.FindAll(o => o.transformParentId == parentId))
        {
            Reporter.Log($"{indent}name='{c.name}' enabled={c.enabled} transformId={c.transformId}");
            DumpDescendants(all, c.transformId, depth + 1, maxDepth);
        }
    }
}
