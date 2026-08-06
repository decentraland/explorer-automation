using ExplorerAutomation.Tests.Views.ExplorePanelSections;

namespace ExplorerAutomation.Tests.Tests;

[AllureSuite("Backpack Emotes Tests")]
[Category("InWorld")]
[Order(10)]
public class BackpackEmotesTests : BaseTest
{
    // NOTES on non-automatable checklist items in this build:
    // - Filter by category: the emotes tab has no category filter UI (only search, the
    //   sort dropdown and the collectibles/smart-wearables view toggles).
    // - Equip/unequip via the grid hover buttons: the hover overlay's Equip/Unequip
    //   Buttons do not respond to synthetic AltTester clicks or taps (verified live).
    //   Equip coverage goes through the double-click path; explicit-button unequip
    //   coverage goes through the slot's Unequip button, which does respond.

    [Test]
    public void TestUnequipAndEquipAllEmoteSlots()
    {
        OpenEmotes();

        Views.ExplorePanel.Backpack.Emotes.UnequipAll();

        for (var i = 0; i < 10; i++)
        {
            Views.ExplorePanel.Backpack.Emotes.SetEmote(i, i);
        }

        for (var i = 0; i < 10; i++)
        {
            Assert.That(Views.ExplorePanel.Backpack.Emotes.GridItems[i].EquippedSlotBadge.IsPresent(), Is.True,
                $"Grid item {i} should show an equipped-slot badge after being equipped");
        }

        Reporter.Log("All emote slots equipped sequentially and badges verified");

        Views.ExplorePanel.Close();
    }

    [Test]
    public void TestSearchAndEquipFistPump()
    {
        OpenEmotes();

        Views.ExplorePanel.Backpack.SearchBar.SetText("Fist Pump");
        Wait(2);

        Views.ExplorePanel.Backpack.Emotes.UnequipEmoteIfPresent(0);
        Views.ExplorePanel.Backpack.Emotes.SetEmote(0, 0);

        Reporter.Log("Fist Pump equipped to slot 0");

        Views.ExplorePanel.Close();
    }

    [Test]
    public void TestUnequipEmoteWithSlotButton()
    {
        OpenEmotes();

        var emotes = Views.ExplorePanel.Backpack.Emotes;

        // Equip a known emote to slot 5 first so the unequip has a deterministic target.
        var gridIndex = emotes.FindUnequippedGridItemIndex();
        emotes.SetEmote(4, gridIndex);
        emotes.GridItems[gridIndex].EquippedSlotBadge.WaitFor();
        Reporter.Log($"Precondition ready — grid item {gridIndex} equipped to slot 5");

        // grid item).
        emotes.ClickSlot(4);
        emotes.ClickUnequip(4);

        emotes.GridItems[gridIndex].EquippedSlotBadge.WaitForGone();
        emotes.Slots[4].EmptyNameLabel.WaitFor();
        Reporter.Log("Emote unequipped via the slot's explicit Unequip button — slot 5 is empty");

        Views.ExplorePanel.Close();
    }

    [Test]
    public void TestEmotesPagination()
    {
        OpenEmotes();

        var emotes = Views.ExplorePanel.Backpack.Emotes;
        emotes.Pager.WaitFor();

        emotes.FirstLoadedGridItem.WaitUntilLoaded();
        var firstPageItem = SelectFirstLoadedItemAndReadName(emotes);
        Reporter.Log($"Page 1 first emote: {firstPageItem}");

        var secondPageItem = FlipPageAndReadFirstItem(emotes, emotes.Pager.NextButton, firstPageItem);
        Reporter.Log($"Page 2 first emote: {secondPageItem}");
        Assert.That(secondPageItem, Is.Not.EqualTo(firstPageItem),
            "First emote on page 2 should differ from first emote on page 1");

        var backOnFirstPage = FlipPageAndReadFirstItem(emotes, emotes.Pager.PreviousButton, secondPageItem);
        Assert.That(backOnFirstPage, Is.EqualTo(firstPageItem),
            "Navigating back should show page 1's first emote again");
        Reporter.Log("Emote pagination forward and back verified");

        Views.ExplorePanel.Close();
    }

    private void OpenEmotes()
    {
        // Open backpack via the keyboard shortcut: more reliable than the sidebar click
        // for the very first interaction post-warmup.
        PressKey(AltKeyCode.I);
        Views.ExplorePanel.WaitFor();
        Views.ExplorePanel.Backpack.EmotesTabButton.Click();
        Views.ExplorePanel.Backpack.Emotes.WaitFor();
    }

    private string SelectFirstLoadedItemAndReadName(ExplorePanelBackpackView.EmotesTab emotes)
    {
        emotes.FirstLoadedGridItem.Click();
        Wait(1);
        return emotes.SelectedItemName.GetText();
    }

    /// <summary>
    /// Clicks a pager arrow and reads the (re-selected) first item name, retrying when
    /// the name has not changed yet — clicks during the grid rebuild are no-ops and
    /// leave a stale name in the info panel.
    /// </summary>
    private string FlipPageAndReadFirstItem(
        ExplorePanelBackpackView.EmotesTab emotes,
        Clickable pagerButton,
        string previousName)
    {
        pagerButton.Click();
        Wait(2);

        for (var attempt = 0; attempt < 3; attempt++)
        {
            emotes.FirstLoadedGridItem.WaitUntilLoaded();
            var name = SelectFirstLoadedItemAndReadName(emotes);
            if (name != previousName)
                return name;

            Wait(2);
        }

        return SelectFirstLoadedItemAndReadName(emotes);
    }
}
