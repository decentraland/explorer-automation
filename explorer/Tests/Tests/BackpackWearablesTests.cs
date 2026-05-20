namespace ExplorerAutomation.Tests.Tests;

[AllureSuite("Backpack Wearables Tests")]
[Category("InWorld")]
[Order(16)]
public class BackpackWearablesTests : BaseTest
{
    // // [Test]
    // // public void TestEquipFirstWearable()
    // // {
    // // Open backpack via the keyboard shortcut: more reliable than the sidebar click
    // // for the very first interaction post-warmup. The dedicated TestOpenBackpackFromSidebar
    // // exercises the click path.
    // PressKey(AltKeyCode.I);
    // Views.ExplorePanel.WaitFor();
    //     // Views.ExplorePanel.Backpack.WearablesTabButton.Click();

    //     //Views.ExplorePanel.Backpack.Wearables.ClickGridItemEquip(0);

    //     // Reporter.Log("First wearable equipped");

    //     Views.ExplorePanel.Close();
    // } //

    [Test]
    public void TestSearchAndEquipWearable()
    {
        // Open backpack via the keyboard shortcut: more reliable than the sidebar click
        // for the very first interaction post-warmup. The dedicated TestOpenBackpackFromSidebar
        // exercises the click path.
        PressKey(AltKeyCode.I);
        Views.ExplorePanel.WaitFor();
        Views.ExplorePanel.Backpack.WearablesTabButton.Click();

        Views.ExplorePanel.Backpack.SearchBar.SetText("Baggy");

        Wait(2);

        Views.ExplorePanel.Backpack.Wearables.ClickGridItemEquip(0);

        Reporter.Log("Wearable equipped from search results");

        Views.ExplorePanel.Close();
    }
}
