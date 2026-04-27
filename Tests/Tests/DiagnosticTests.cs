namespace ExplorerAutomation.Tests.Tests;

/// <summary>
/// Temporary diagnostic — delete after MainMenuView IDs are fixed.
/// Run with: dotnet test Tests/ --filter "DiagnoseMainMenuSidebar"
/// </summary>
[TestFixture]
public class DiagnosticTests
{
    private AltDriver Driver => CommonStuff.AltDriver;

    [Test]
    public void DiagnoseMainMenuSidebar()
    {
        var knownIds = new Dictionary<string, string>
        {
            { "ProfileButton",         "578d9b4e-0531-4cb3-abd7-aa79506c1b3e" },
            { "NotificationsButton",   "6c66dc7b-5c51-4b1c-bd27-0814d9c837ae" },
            { "EventsButton",          "d5ac3302-135f-4d89-9af3-56df31776664" },
            { "PlacesButton",          "bcd4b7ed-97f9-419c-8df8-d8a0218388d2" },
            { "CommunitiesButton",     "9335caa1-070d-47cd-92f8-2ab0bee06003" },
            { "MapButton",             "2b8e4546-23be-4e65-973b-7928eb02f238" },
            { "BackpackButton",        "bab6108c-7cce-45a1-9bcd-40412c1f435e" },
            { "MarketplaceButton",     "31e1fb4b-d737-4351-bc21-97e00f715ebe" },
            { "GalleryButton",         "6d5004d7-5a52-4250-b98a-5799f5e8c011" },
            { "SettingsButton",        "e4146db9-0b45-4c41-8cf0-2cde69a0ce0a" },
            { "ControlsButton",        "6f7c9619-29d4-4dfd-8aad-f8b10f56939a" },
            { "HelpButton",            "c02afb7d-0abf-405e-9ecc-48f8cf439f42" },
            { "SidebarSettingsButton", "a7a98fe6-eca1-4f67-996e-2049c9e020bb" },
        };

        // Validate existing IDs
        Console.WriteLine("=== Validating known button IDs ===");
        foreach (var (name, id) in knownIds)
        {
            try
            {
                var obj = Driver.FindObject(By.ID, id);
                Console.WriteLine($"[OK]    {name}: {id} (name={obj.name})");
            }
            catch
            {
                Console.WriteLine($"[STALE] {name}: {id} — not found");
            }
        }

        // Search for sidebar container by walking up from any found button
        Console.WriteLine("\n=== Walking parent chain to find sidebar container ===");
        AltObject anchor = null;
        string anchorName = null;
        foreach (var (name, id) in knownIds)
        {
            try
            {
                anchor = Driver.FindObject(By.ID, id);
                anchorName = name;
                break;
            }
            catch { }
        }

        if (anchor == null)
        {
            Console.WriteLine("No known button found — searching by name patterns...");
            foreach (var candidate in new[] { "Backpack", "Profile", "Events", "MainMenu", "Sidebar" })
            {
                try
                {
                    anchor = Driver.FindObject(By.NAME, candidate);
                    anchorName = candidate;
                    Console.WriteLine($"Found by name '{candidate}': id={anchor.id} path={anchor.transformPath}");
                    break;
                }
                catch { }
            }
        }

        if (anchor != null)
        {
            Console.WriteLine($"Anchor: {anchorName} id={anchor.id} path={anchor.transformPath}");
            Console.WriteLine($"  parent id={anchor.transformParentId}");

            // Walk up two levels to find the container
            try
            {
                var parent = Driver.FindObject(By.ID, anchor.transformParentId.ToString());
                Console.WriteLine($"Parent:  name={parent.name} id={parent.id} path={parent.transformPath}");
                Console.WriteLine($"  parent id={parent.transformParentId}");

                var grandparent = Driver.FindObject(By.ID, parent.transformParentId.ToString());
                Console.WriteLine($"Grandparent: name={grandparent.name} id={grandparent.id} path={grandparent.transformPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could not walk parents: {ex.Message}");
            }
        }

        // Also dump all elements whose name contains sidebar/menu keywords
        Console.WriteLine("\n=== Elements matching 'sidebar', 'menu', 'hud', 'navigation' ===");
        try
        {
            var allElements = Driver.GetAllElements();
            foreach (var el in allElements)
            {
                var n = el.name.ToLower();
                if (n.Contains("sidebar") || n.Contains("mainmenu") || n.Contains("sidemenu")
                    || n.Contains("navigation") || n.Contains("hudmenu"))
                {
                    Console.WriteLine($"  name={el.name} id={el.id} path={el.transformPath}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GetAllElements failed: {ex.Message}");
        }
    }
}
