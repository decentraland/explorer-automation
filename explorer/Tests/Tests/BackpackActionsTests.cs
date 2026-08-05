namespace ExplorerAutomation.Tests.Tests;

[AllureSuite("Backpack Actions Tests")]
[Category("InWorld")]
[Order(16)]
public class BackpackActionsTests : BaseTest
{
    #region Wearables

    [Test]
    public void TestEquipWearableBySlot()
    {
        PressKey(AltKeyCode.I);
        Views.ExplorePanel.WaitFor();
        Views.ExplorePanel.Backpack.WearablesTabButton.Click();
        Views.ExplorePanel.Backpack.Wearables.AvatarSlotHair.Click();
        Reporter.Log("Hair slot clicked — grid filtered to hair wearables");

        Wait(3);

        Views.ExplorePanel.Backpack.Wearables.ClickFirstBackpackItem();
        Reporter.Log("First BackpackItem in grid clicked and equipped");

        Wait(3);

        Views.ExplorePanel.Close();
    }

    [Test]
    public void TestSearchWearable()
    {
        PressKey(AltKeyCode.I);
        Views.ExplorePanel.WaitFor();
        Views.ExplorePanel.Backpack.WearablesTabButton.Click();
        Views.ExplorePanel.Backpack.SearchBar.SetText("Baggy");

        Wait(2);

        Reporter.Log("Search results displayed for 'Baggy'");

        Views.ExplorePanel.Close();
    }

    #endregion

    #region Emotes

    [Test]
    public void TestUnequipAndEquipAllEmoteSlots()
    {
        PressKey(AltKeyCode.I);
        Views.ExplorePanel.WaitFor();
        Views.ExplorePanel.Backpack.EmotesTabButton.Click();
        Views.ExplorePanel.Backpack.Emotes.UnequipAll();

        for (var i = 0; i < 10; i++)
        {
            Views.ExplorePanel.Backpack.Emotes.SetEmote(i, i);
        }

        Reporter.Log("All emote slots equipped sequentially");

        Views.ExplorePanel.Close();
    }

    [Test]
    public void TestSearchAndEquipFistPump()
    {
        PressKey(AltKeyCode.I);
        Views.ExplorePanel.WaitFor();
        Views.ExplorePanel.Backpack.EmotesTabButton.Click();
        Views.ExplorePanel.Backpack.SearchBar.SetText("Fist Pump");

        Wait(3);

        Views.ExplorePanel.Backpack.Emotes.UnequipEmoteIfPresent(0);
        Views.ExplorePanel.Backpack.Emotes.SetEmote(0, 0);

        Reporter.Log("Fist Pump equipped to slot 0");

        Views.ExplorePanel.Close();
    }

    #endregion

    #region Outfits

    [Test]
    public void TestOpenSavedOutfitsTab()
    {
        PressKey(AltKeyCode.I);
        Views.ExplorePanel.WaitFor();

        Assert.IsTrue(Views.ExplorePanel.Backpack.WearablesTabButton.IsPresent(), "Wearables tab button should be visible");
        Assert.IsTrue(Views.ExplorePanel.Backpack.EmotesTabButton.IsPresent(), "Emotes tab button should be visible");
        Assert.IsTrue(Views.ExplorePanel.Backpack.SavedOutfitsTabButton.IsPresent(), "Saved Outfits tab button should be visible");
        Reporter.Log("All backpack tabs present");

        Views.ExplorePanel.Backpack.SavedOutfitsTabButton.Click();
        Reporter.Log("Saved Outfits tab clicked");

        Wait(3);

        Views.ExplorePanel.Close();
    }

    [Test]
    public void TestEquipFirstSavedOutfit()
    {
        PressKey(AltKeyCode.I);
        Views.ExplorePanel.WaitFor();
        Views.ExplorePanel.Backpack.SavedOutfitsTabButton.Click();

        Views.ExplorePanel.Backpack.SavedOutfits.GridItems[0].WaitFor();
        Reporter.Log("Outfits grid visible — clicking first outfit");

        Views.ExplorePanel.Backpack.SavedOutfits.ClickGridItem(0);
        Views.ExplorePanel.Backpack.SavedOutfits.ClickGridItemEquip(0);
        Reporter.Log("First saved outfit equipped");

        Views.ExplorePanel.Close();
    }

    #endregion
}
