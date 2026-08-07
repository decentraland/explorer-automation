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

    // Per-attempt budget for a confirmed equip — see BackpackEmotesTests for the rationale.
    private const double EQUIP_SETTLE_PER_ATTEMPT = 10;
    // Re-reads allowed while waiting for the grid's leading item to stop changing.
    private const int SETTLE_READS = 3;

    [Test]
    public void TestEquipWearableBySlot()
    {
        OpenWearables();

        Views.ExplorePanel.Backpack.Wearables.EnsureHairCategory();
        Reporter.Log("Grid filtered to hair wearables");
        Wait(2);

        // Pick a hair that is not currently equipped so the test is re-runnable.
        var target = Views.ExplorePanel.Backpack.Wearables.FindUnequippedGridItem();
        EquipUntilShown(target);

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
        EquipUntilShown(target);

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
        Wait(2);

        // shows a matching item. Settled read: the grid pool keeps pre-search tiles enabled
        // while results stream in, so a single read can land on one the search is about to
        // replace — the same exposure as the pagination round-trip, just cheaper to hit here
        // because every surviving tile matches the query.
        var itemName = ReadSettledFirstItemName(Views.ExplorePanel.Backpack.Wearables);
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

        var firstPageItem = ReadSettledFirstItemName(wearables);
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
    /// Equips the item, retrying until the hover overlay confirms it. The equip double-click
    /// is dropped when it lands during a grid re-bind, so a single attempt followed by a
    /// fixed wait reads back unequipped — the failure behind TestDoubleClickEquipWearable on
    /// CI runs 31176916555 and 31180360091. Re-equipping an already-equipped item is a no-op.
    /// </summary>
    private void EquipUntilShown(ExplorePanelBackpackView.BackpackGridItem item)
    {
        ClickUntil(() => item.DoubleClickEquip(),
                   () => item.IsEquipped(verificationShot: false),
                   timeoutPerAttempt: EQUIP_SETTLE_PER_ATTEMPT);
        Wait(2);
    }

    /// <summary>
    /// Clicks the item and reads the info panel name, retrying because a click on a tile
    /// that is still refreshing is a no-op and would leave a stale name in the panel.
    /// </summary>
    private string SelectItemAndReadName(
        ExplorePanelBackpackView.BackpackGridItem item,
        Readable nameLabel)
    {
        item.Click();
        Wait(1);
        return nameLabel.GetText();
    }

    /// <summary>
    /// Reads the leading item's name until two consecutive reads agree, so a baseline is
    /// pinned to a grid that has stopped moving. FirstLoadedGridItem is an unindexed path,
    /// and the grid pool keeps stale tiles enabled while results stream in, so a single read
    /// can land on a tile the page is about to replace — the defect that failed the emotes
    /// pagination round-trip on CI run 31164127596 and is latent here.
    /// Selection deliberately stays on FirstLoadedGridItem rather than an indexed tile:
    /// clicking a tile ROOT leaves the info panel unpopulated (proven on run 31176916555),
    /// and only this locator is rooted on the selectable FullBackpack child.
    /// </summary>
    private string ReadSettledFirstItemName(ExplorePanelBackpackView.WearablesTab wearables)
    {
        wearables.FirstLoadedGridItem.WaitUntilLoaded(SlowChassis.SETTLE_TIMEOUT);
        var name = SelectItemAndReadName(wearables.FirstLoadedGridItem, wearables.SelectedItemName);

        for (var attempt = 0; attempt < SETTLE_READS; attempt++)
        {
            Wait(1);
            wearables.FirstLoadedGridItem.WaitUntilLoaded(SlowChassis.SETTLE_TIMEOUT);
            var reread = SelectItemAndReadName(wearables.FirstLoadedGridItem, wearables.SelectedItemName);
            if (reread == name)
                return name;

            name = reread;
        }

        return name;
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
            wearables.FirstLoadedGridItem.WaitUntilLoaded(SlowChassis.SETTLE_TIMEOUT);
            var name = SelectItemAndReadName(wearables.FirstLoadedGridItem, wearables.SelectedItemName);
            if (name != previousName)
                return name;

            Wait(2);
        }

        // Still reading the outgoing page's name: return a settled read so the caller's
        // assertion fails against what the grid actually ended up showing.
        return ReadSettledFirstItemName(wearables);
    }
}
