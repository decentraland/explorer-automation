using ExplorerAutomation.Tests.Views.ExplorePanelSections;

namespace ExplorerAutomation.Tests.Tests;

[AllureSuite("Backpack Outfits Tests")]
[Category("InWorld")]
[Order(17)]
public class BackpackOutfitsTests : BaseTest
{
    // NOTE: outfit UNEQUIP cannot be automated in this build — the outfit slots'
    // ButtonUnequip is never enabled because the equipped state (EquippedBackground)
    // never activates, even right after equipping an outfit and reopening the panel.

    // Wall-clock budget for the equip to show up, matching BackpackWearablesTests.
    private const double EQUIP_CONFIRM_TIMEOUT = 8;

    [Test, Order(1)]
    public void TestOpenSavedOutfitsTab()
    {
        OpenBackpack();

        Assert.That(Views.ExplorePanel.Backpack.WearablesTabButton.IsPresent(), Is.True,
            "Wearables tab button should be visible");
        Assert.That(Views.ExplorePanel.Backpack.EmotesTabButton.IsPresent(), Is.True,
            "Emotes tab button should be visible");
        Assert.That(Views.ExplorePanel.Backpack.SavedOutfitsTabButton.IsPresent(), Is.True,
            "Saved Outfits tab button should be visible");
        Reporter.Log("All backpack tabs present");

        Views.ExplorePanel.Backpack.OpenSavedOutfits();
        Views.ExplorePanel.Backpack.SavedOutfits.Slots[0].WaitFor();
        Reporter.Log("Saved Outfits tab open with outfit slots visible");

        Views.ExplorePanel.Close();
    }

    [Test, Order(2)]
    public void TestSaveOutfit()
    {
        OpenBackpack();
        Views.ExplorePanel.Backpack.OpenSavedOutfits();

        var slot = Views.ExplorePanel.Backpack.SavedOutfits.FindFirstEmptySlot();
        if (slot == null)
        {
            // All five slots full (e.g. left over from earlier runs) — free the last one
            // so the save flow can be exercised.
            Reporter.Log("All outfit slots full — deleting slot 5 to make room");
            slot = Views.ExplorePanel.Backpack.SavedOutfits.Slots[^1];
            DeleteOutfitSlot(slot);
        }

        slot.Save();
        slot.FullState.WaitFor(30);
        Reporter.Log("Current look saved into an outfit slot");

        Views.ExplorePanel.Close();
    }

    [Test, Order(3)]
    public void TestEquipFirstSavedOutfit()
    {
        // Was gated on the hair.IsEquipped() precondition reading false after an equip the
        // test had already confirmed. Both halves of that are gone: the equip presses a button
        // rather than hoping for a double-click, and IsEquipped reads the client's own flag
        // instead of hover-probing an overlay whose PointerEnter could be swallowed.
        OpenBackpack();
        Views.ExplorePanel.Backpack.OpenSavedOutfits();
        Views.ExplorePanel.Backpack.SavedOutfits.EnsureFirstSlotSaved();

        // Diverge the avatar from the saved outfit: equip a different hair.
        Views.ExplorePanel.Backpack.OpenCategories();
        Views.ExplorePanel.Backpack.Wearables.EnsureHairCategory();
        Wait(2);
        var hair = Views.ExplorePanel.Backpack.Wearables.FindUnequippedGridItem();
        // No retry — see BackpackWearablesTests.EquipUntilShown. Polls the flag rather than
        // IsEquipped so the loop does not re-hover.
        hair.Equip();
        WaitUntil(hair.ReadEquippedFlag, EQUIP_CONFIRM_TIMEOUT);
        Wait(2);
        // The Equip button only equips, so select the tile to make the info panel name it.
        hair.Click();
        var hairName = Views.ExplorePanel.Backpack.Wearables.SelectedItemName.GetText();
        Assert.That(hair.IsEquipped(), Is.True, "Precondition: the alternative hair should be equipped");
        Reporter.Log($"Avatar diverged from saved outfit (equipped hair '{hairName}')");

        Views.ExplorePanel.Backpack.OpenSavedOutfits();
        Views.ExplorePanel.Backpack.SavedOutfits.Slots[0].Equip();
        Wait(3);

        // Re-apply the hair filter (it does not reliably survive the sub-tab switch).
        // The grid keeps its deterministic sort, so the same grid index still points at
        // the hair equipped above — verify identity via the info panel before asserting.
        Views.ExplorePanel.Backpack.OpenCategories();
        Views.ExplorePanel.Backpack.Wearables.EnsureHairCategory();
        Wait(2);
        hair.Click();
        Wait(1);
        var reselectedName = Views.ExplorePanel.Backpack.Wearables.SelectedItemName.GetText();
        Assert.That(reselectedName, Is.EqualTo(hairName),
            "Grid should still show the same hair at the same index after the sub-tab switch");
        Assert.That(hair.IsEquipped(), Is.False,
            "Equipping the saved outfit should have reverted the hair change");
        Reporter.Log("Saved outfit equipped — avatar reverted to the saved look");

        Views.ExplorePanel.Close();
    }

    [Test, Order(4)]
    public void TestDeleteOutfit()
    {
        OpenBackpack();
        Views.ExplorePanel.Backpack.OpenSavedOutfits();

        // Keep at least one saved outfit around: make sure slot 1 is saved, then create
        // a throwaway outfit in the first empty slot and delete that one.
        Views.ExplorePanel.Backpack.SavedOutfits.EnsureFirstSlotSaved();

        var slot = Views.ExplorePanel.Backpack.SavedOutfits.FindFirstEmptySlot();
        if (slot == null)
        {
            Reporter.Log("All outfit slots full — using the last slot as the delete target");
            slot = Views.ExplorePanel.Backpack.SavedOutfits.Slots[^1];
        }
        else
        {
            slot.Save();
            slot.FullState.WaitFor(30);
            Reporter.Log("Throwaway outfit saved");
        }

        DeleteOutfitSlot(slot);
        Reporter.Log("Outfit deleted and slot returned to empty");

        Views.ExplorePanel.Close();
    }

    private void OpenBackpack()
    {
        PressKey(AltKeyCode.I);
        Views.ExplorePanel.WaitFor();
        Views.ExplorePanel.Backpack.WearablesTabButton.Click();
        Views.ExplorePanel.Backpack.WaitFor();
    }

    private void DeleteOutfitSlot(ExplorePanelBackpackView.SavedOutfitsTab.OutfitSlot slot)
    {
        slot.Delete();
        Views.ConfirmationDialog.WaitFor();
        Views.ConfirmationDialog.YesButton.Click();
        Views.ConfirmationDialog.WaitForGone();
        slot.EmptyState.WaitFor(30);
    }
}
