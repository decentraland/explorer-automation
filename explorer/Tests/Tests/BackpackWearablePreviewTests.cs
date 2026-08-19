using ExplorerAutomation.Tests.Views.ExplorePanelSections;

namespace ExplorerAutomation.Tests.Tests;

// Not [Category("InWorld")]: these run in CI's dedicated self-preview pass, whose client
// boots with --self-preview-wearables so the throwaway account can load on-chain wearables.
[AllureSuite("Backpack Wearable Preview Tests")]
[Category("SelfPreview")]
[Order(18)]
public class BackpackWearablePreviewTests : BaseTest
{
    private const double THUMBNAIL_LOAD_TIMEOUT = 30;
    private const double EQUIP_CONFIRM_TIMEOUT = 8;
    // CSV of wearable URNs the client was launched with via --self-preview-wearables.
    private const string SELF_PREVIEW_ENV = "SELF_PREVIEW_WEARABLES";

    [Test, Order(1)]
    public void TestL1WearableSlotThumbnail() =>
        VerifyPreviewWearableLoads("urn:decentraland:ethereum:collections-v1:", "L1 (Ethereum)");

    [Test, Order(2)]
    public void TestL2WearableSlotThumbnail() =>
        VerifyPreviewWearableLoads("urn:decentraland:matic:collections-v2:", "L2 (Polygon)");

    /// <summary>
    /// Equips the self-preview wearable matching <paramref name="urnPrefix"/> from the grid
    /// and verifies its avatar slot ends up with a visible thumbnail.
    /// </summary>
    private void VerifyPreviewWearableLoads(string urnPrefix, string tier)
    {
        var urn = PreviewUrnFor(urnPrefix);
        if (urn == null)
            Assert.Ignore($"{SELF_PREVIEW_ENV} carries no {tier} URN — client not launched in self-preview mode");

        OpenWearables();
        var wearables = Views.ExplorePanel.Backpack.Wearables;

        var tile = wearables.FindGridItemWithUrn(urn);
        if (!tile.IsEquipped(verificationShot: false))
        {
            tile.Equip();
            Assert.That(WaitUntil(tile.ReadEquippedFlag, EQUIP_CONFIRM_TIMEOUT), Is.True,
                $"The {tier} preview wearable should report equipped after pressing its Equip button");
        }

        var slot = FindSlotWithUrn(wearables, urn);
        Assert.That(slot, Is.Not.Null, $"An avatar slot should hold '{urn}' after equipping");
        Assert.That(WaitUntil(() => wearables.IsSlotThumbnailLoaded(slot), THUMBNAIL_LOAD_TIMEOUT),
            Is.True, $"{tier} slot thumbnail should be visible for '{urn}'");
        Reporter.Log($"{tier} wearable '{urn}' equipped, slot thumbnail loaded");

        Views.ExplorePanel.Close();
    }

    private static string PreviewUrnFor(string urnPrefix) =>
        (Environment.GetEnvironmentVariable(SELF_PREVIEW_ENV) ?? string.Empty)
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .FirstOrDefault(u => u.StartsWith(urnPrefix, StringComparison.Ordinal));

    private static Clickable FindSlotWithUrn(
        ExplorePanelBackpackView.WearablesTab wearables, string urn)
    {
        var candidates = new[]
        {
            wearables.AvatarSlotTop,
            wearables.AvatarSlotBottom,
            wearables.AvatarSlotShoes,
            wearables.AvatarSlotHat,
            wearables.AvatarSlotMask,
            wearables.AvatarSlotEyewear,
            wearables.AvatarSlotHandwear,
            wearables.AvatarSlotHair,
        };

        foreach (var slot in candidates)
        {
            var slotUrn = wearables.GetSlotUrn(slot);
            // Owned on-chain wearables may carry a :tokenId suffix on the slot.
            if (slotUrn == urn || (slotUrn != null && slotUrn.StartsWith(urn + ":", StringComparison.Ordinal)))
                return slot;
        }

        return null;
    }

    private void OpenWearables()
    {
        PressKey(AltKeyCode.I);
        Views.ExplorePanel.WaitFor();
        Views.ExplorePanel.Backpack.WearablesTabButton.Click();
        Views.ExplorePanel.Backpack.Wearables.WaitFor();
    }
}
