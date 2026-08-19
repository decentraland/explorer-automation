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

            Assert.That(WaitUntil(() => wearables.IsSlotThumbnailLoaded(slot), THUMBNAIL_LOAD_TIMEOUT),
                Is.True, $"{name} slot thumbnail should be visible");
            Reporter.Log($"Base wearable slot {name} verified: URN present, thumbnail loaded");
        }

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
