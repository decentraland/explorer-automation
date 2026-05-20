namespace ExplorerAutomation.Tests.Tests;

[AllureSuite("Backpack Outfits Tests")]
[Category("InWorld")]
[Order(17)]
public class BackpackOutfitsTests : BaseTest
{
    [Test, Order(1)]
    public void TestOpenSavedOutfitsTab()
    {
        PressKey(AltKeyCode.I);
        Views.ExplorePanel.WaitFor();

        // Verify all three backpack tab buttons are present before interacting
        Assert.IsTrue(Views.ExplorePanel.Backpack.WearablesTabButton.IsPresent(), "Wearables tab button should be visible");
        Assert.IsTrue(Views.ExplorePanel.Backpack.EmotesTabButton.IsPresent(), "Emotes tab button should be visible");
        Assert.IsTrue(Views.ExplorePanel.Backpack.SavedOutfitsTabButton.IsPresent(), "Saved Outfits tab button should be visible");
        Reporter.Log("All backpack tabs present");

        Views.ExplorePanel.Backpack.SavedOutfitsTabButton.Click();
        Reporter.Log("Saved Outfits tab clicked");
        Wait(3);

        Views.ExplorePanel.Close();
    }

    [Test, Order(2)]
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
}
