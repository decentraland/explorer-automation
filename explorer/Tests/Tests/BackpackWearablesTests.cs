using ExplorerAutomation.Tests.Views.ExplorePanelSections;

namespace ExplorerAutomation.Tests.Tests;

[AllureSuite("Backpack Wearables Tests")]
[Category("InWorld")]
[Order(16)]
public class BackpackWearablesTests : BaseTest
{
    // NOTE: the grid hover overlay's Equip/Unequip Buttons do not respond to synthetic
    // AltTester input in this build, so both equip tests go through the double-click
    // path (BackpackItemView treats clickCount == 2 as Equip) and use the hover overlay
    // only as a read-only equipped-state indicator.

    [Test]
    public void TestEquipWearableBySlot()
    {
        OpenWearables();

        Views.ExplorePanel.Backpack.Wearables.EnsureHairCategory();
        Reporter.Log("Grid filtered to hair wearables");
        Wait(2);

        // Pick a hair that is not currently equipped so the test is re-runnable.
        var target = Views.ExplorePanel.Backpack.Wearables.FindUnequippedGridItem();
        target.DoubleClickEquip();
        WaitUntil(() => target.IsEquipped(verificationShot: false));

        Assert.That(target.IsEquipped(), Is.True,
            "Grid item should show the hover Unequip indicator after being equipped");
        Reporter.Log("Wearable equipped from the hair category grid");

        Views.ExplorePanel.Close();
    }

    [Test]
    public void TestDoubleClickEquipWearable()
    {
        OpenWearables();

        Views.ExplorePanel.Backpack.Wearables.EnsureHairCategory();
        Reporter.Log("Grid filtered to hair wearables");
        Wait(2);

        var target = Views.ExplorePanel.Backpack.Wearables.FindUnequippedGridItem();
        target.DoubleClickEquip();
        WaitUntil(() => target.IsEquipped(verificationShot: false));

        Assert.That(target.IsEquipped(), Is.True,
            "Grid item should show the hover Unequip indicator after double-click equip");
        Reporter.Log("Wearable equipped via double-click");

        Views.ExplorePanel.Close();
    }

    [Test]
    public void TestSearchWearable()
    {
        OpenWearables();

        // "Punk" is a base-collection hair every account owns.
        Views.ExplorePanel.Backpack.SearchBar.SetText("Punk");
        // Give the grid a beat to refilter before the first read: a click during the
        // transition can land on a stale (pre-search) tile, or briefly find no item
        // selected at all while the info panel is between states — verified live, this
        // is NOT redundant with WaitUntilLoaded() below (that alone returns true on the
        // stale tile too).
        Wait(2);

        // shows a matching item.
        var result = Views.ExplorePanel.Backpack.Wearables.FirstLoadedGridItem;
        result.WaitUntilLoaded();
        var itemName = SelectItemAndReadName(result, Views.ExplorePanel.Backpack.Wearables.SelectedItemName);
        Assert.That(itemName.ToLowerInvariant(), Does.Contain("punk"),
            $"Selected search result '{itemName}' should match the search term 'Punk'");
        Reporter.Log($"Search returned and selected '{itemName}'");

        Views.ExplorePanel.Close();
    }

    [Test]
    public void TestWearablesPagination()
    {
        OpenWearables();

        var wearables = Views.ExplorePanel.Backpack.Wearables;
        wearables.Pager.WaitFor();

        wearables.FirstLoadedGridItem.WaitUntilLoaded();
        var firstPageItem = SelectItemAndReadName(wearables.FirstLoadedGridItem, wearables.SelectedItemName);
        Reporter.Log($"Page 1 first item: {firstPageItem}");

        var secondPageItem = FlipPageAndReadFirstItem(wearables, wearables.Pager.NextButton, firstPageItem);
        Reporter.Log($"Page 2 first item: {secondPageItem}");
        Assert.That(secondPageItem, Is.Not.EqualTo(firstPageItem),
            "First item on page 2 should differ from first item on page 1");

        var backOnFirstPage = FlipPageAndReadFirstItem(wearables, wearables.Pager.PreviousButton, secondPageItem);
        Assert.That(backOnFirstPage, Is.EqualTo(firstPageItem),
            "Navigating back should show page 1's first item again");
        Reporter.Log("Pagination forward and back verified");

        Views.ExplorePanel.Close();
    }

    private void OpenWearables()
    {
        // Open backpack via the keyboard shortcut: more reliable than the sidebar click
        // for the very first interaction post-warmup. The dedicated TestOpenBackpackFromSidebar
        // exercises the click path.
        PressKey(AltKeyCode.I);
        Views.ExplorePanel.WaitFor();
        Views.ExplorePanel.Backpack.WearablesTabButton.Click();
        Views.ExplorePanel.Backpack.Wearables.WaitFor();
    }

    /// <summary>
    /// Clicks the item and reads the info panel name, polling until the label shows text
    /// that differs from <paramref name="previousName"/> (or any text, on the first-ever
    /// selection) — a click on a tile that is still refreshing is a no-op and would
    /// otherwise leave a stale name in the panel.
    /// </summary>
    private string SelectItemAndReadName(
        ExplorePanelBackpackView.BackpackGridItem item,
        Readable nameLabel,
        string previousName = null)
    {
        item.Click();
        return nameLabel.WaitForText(text => !string.IsNullOrEmpty(text) && text != previousName);
    }

    /// <summary>
    /// Clicks a pager arrow and reads the (re-selected) first item name, retrying once
    /// when the name has not changed yet — the grid rebuild can swallow the first click.
    /// </summary>
    private string FlipPageAndReadFirstItem(
        ExplorePanelBackpackView.WearablesTab wearables,
        Clickable pagerButton,
        string previousName)
    {
        pagerButton.Click();
        Wait(2);

        for (var attempt = 0; attempt < 3; attempt++)
        {
            wearables.FirstLoadedGridItem.WaitUntilLoaded();
            var name = SelectItemAndReadName(wearables.FirstLoadedGridItem, wearables.SelectedItemName, previousName);
            if (name != previousName)
                return name;

            Wait(2);
        }

        return SelectItemAndReadName(wearables.FirstLoadedGridItem, wearables.SelectedItemName);
    }
}
