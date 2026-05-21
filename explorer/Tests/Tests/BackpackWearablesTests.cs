namespace ExplorerAutomation.Tests.Tests;

[AllureSuite("Backpack Wearables Tests")]
[Category("InWorld")]
[Order(16)]
public class BackpackWearablesTests : BaseTest
{

    [Test]
    public void TestEquipWearableBySlot()
    {
        PressKey(AltKeyCode.I);
        Views.ExplorePanel.WaitFor();
        Views.ExplorePanel.Backpack.WearablesTabButton.Click();

        Views.ExplorePanel.Backpack.Wearables.AvatarSlotHair.Click();
        Reporter.Log("Hair slot clicked — grid filtered to hair wearables");

        Wait(2);

        Views.ExplorePanel.Backpack.Wearables.ClickFirstBackpackItem();
        Reporter.Log("First BackpackItem in grid clicked and equipped");

        Wait(2);

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
}
