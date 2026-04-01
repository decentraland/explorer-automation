namespace ExplorerAutomation.Tests.Tests;

[AllureSuite("Backpack Emotes Tests")]
public class BackpackEmotesTests : BaseTest
{
    [Test]
    public void TestUnequipAndEquipAllEmoteSlots()
    {
        Views.MainMenu.BackpackButton.Click();
        Views.ExplorePanel.WaitFor();
        Views.ExplorePanel.Backpack.EmotesTabButton.Click();

        // Unequip all slots (safely skips already empty ones)
        Views.ExplorePanel.Backpack.Emotes.UnequipAll();

        // Equip emotes sequentially: slot 0 -> grid 0, slot 1 -> grid 1, etc.
        for (var i = 0; i < 10; i++)
        {
            Views.ExplorePanel.Backpack.Emotes.SetEmote(i, i);
        }

        Reporter.Log("All emote slots equipped sequentially");

        Views.ExplorePanel.CloseButton.Click();
        Views.ExplorePanel.WaitForGone();
    }

    [Test]
    public void TestSearchAndEquipFistPump()
    {
        Views.MainMenu.BackpackButton.Click();
        Views.ExplorePanel.WaitFor();
        Views.ExplorePanel.Backpack.EmotesTabButton.Click();

        Views.ExplorePanel.Backpack.SearchBar.SetText("Fist Pump");
        
        Wait(2);

        // Unequip slot 0 if it has an emote
        Views.ExplorePanel.Backpack.Emotes.UnequipEmoteIfPresent(0);

        // Equip the first (and only) grid item to slot 0
        Views.ExplorePanel.Backpack.Emotes.SetEmote(0, 0);

        Reporter.Log("Bezier Dance equipped to slot 0");

        Views.ExplorePanel.CloseButton.Click();
        Views.ExplorePanel.WaitForGone();
    }
}
