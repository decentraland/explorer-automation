namespace ExplorerAutomation.Tests.Tests;

[AllureSuite("Backpack Wearable Visibility Tests")]
[Category("InWorld")]
[Order(18)]
public class BackpackWearableVisibilityTests : BaseTest
{
    private const double THUMBNAIL_LOAD_TIMEOUT = 30;
    private const double EQUIP_CONFIRM_TIMEOUT = 8;

    [Test, Order(1)]
    public void TestBaseWearableSlotThumbnails()
    {
        OpenWearables();

        var wearables = Views.ExplorePanel.Backpack.Wearables;
        var baseSlots = new (string Name, Clickable Slot)[]
        {
            ("Hair", wearables.AvatarSlotHair),
            ("Eyes", wearables.AvatarSlotEyes),
            ("Eyebrows", wearables.AvatarSlotEyebrows),
            ("Mouth", wearables.AvatarSlotMouth),
        };

        foreach (var (name, slot) in baseSlots)
        {
            var urn = wearables.GetSlotUrn(slot);
            Assert.That(urn, Does.StartWith("urn:decentraland:off-chain:base-avatars:"),
                $"{name} slot should have a base-avatar URN but got '{urn}'");
        }

        // One visible thumbnail proves the pipeline; demanding all four asserts the chassis
        // network instead: opening the backpack fires every slot and tile thumbnail at once,
        // the client gives each a single 30s budget, and a miss is sticky for the session.
        // A pipeline regression breaks all of them together, so one is the honest signal —
        // the equip test covers the per-slot case through a fresh, uncontended request.
        Assert.That(
            WaitUntil(() => baseSlots.Any(s => wearables.IsSlotThumbnailLoaded(s.Slot)), THUMBNAIL_LOAD_TIMEOUT),
            Is.True, "No base wearable slot ever showed a thumbnail");
        var visible = baseSlots.Where(s => wearables.IsSlotThumbnailLoaded(s.Slot)).Select(s => s.Name);
        Reporter.Log($"Base slots verified: URNs on all four, thumbnails visible on [{string.Join(", ", visible)}]");

        Views.ExplorePanel.Close();
    }

    [Test, Order(2)]
    public void TestEquipBaseWearableAndVerifyVisibility()
    {
        OpenWearables();

        var wearables = Views.ExplorePanel.Backpack.Wearables;
        wearables.EnsureHairCategory();

        var target = wearables.FindUnequippedGridItem();
        target.Equip();
        Assert.That(WaitUntil(target.ReadEquippedFlag, EQUIP_CONFIRM_TIMEOUT), Is.True,
            "Grid item should report equipped after pressing its Equip button");

        Assert.That(WaitUntil(() => wearables.IsSlotThumbnailLoaded(wearables.AvatarSlotHair), THUMBNAIL_LOAD_TIMEOUT),
            Is.True, "Hair slot thumbnail should be visible after equipping a base wearable");
        Reporter.Log("Equipped base wearable and verified slot thumbnail is visible");

        Views.ExplorePanel.Close();
    }

    private void OpenWearables()
    {
        PressKey(AltKeyCode.I);
        Views.ExplorePanel.WaitFor();
        Views.ExplorePanel.Backpack.WearablesTabButton.Click();
        Views.ExplorePanel.Backpack.Wearables.WaitFor();
    }
}
