namespace ExplorerAutomation.Tests.Tests;

[AllureSuite("Backpack Wearables Tests")]
[Category("InWorld")]
[Order(16)]
public class BackpackWearablesTests : BaseTest
{
    [Test]
    public void TestEquipFirstWearable()
    {
        PressKey(AltKeyCode.I);
        Views.ExplorePanel.WaitFor();
        Views.ExplorePanel.Backpack.WearablesTabButton.Click();

        // Verify the wearable grid has loaded at least one item before interacting
        Views.ExplorePanel.Backpack.Wearables.GridItems[0].WaitFor();
        Reporter.Log("Wearable grid visible — hovering first item to reveal equip button");

        Views.ExplorePanel.Backpack.Wearables.ClickGridItemEquip(0);
        Reporter.Log("First wearable equipped");

        Views.ExplorePanel.Close();
    }
}
