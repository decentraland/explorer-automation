using ExplorerAutomation.Tests.Views.ExplorePanelSections;

namespace ExplorerAutomation.Tests.Tests;

[AllureSuite("Backpack Wearables Tests")]
[Category("InWorld")]
[Order(16)]
public class BackpackWearablesTests : BaseTest
{
    // NOTE: equipping goes through the overlay's Equip button. It reaches the same
    // BackpackItemView.OnEquip the double-click does, without depending on Unity raising
    // clickCount == 2, which never became reliable through the driver.

    private const double EQUIP_CONFIRM_TIMEOUT = 8;
    private const int PAGE_FLIP_ATTEMPTS = 3;

    [Test]
    public void TestEquipWearableFromGrid()
    {
        OpenWearables();

        Views.ExplorePanel.Backpack.Wearables.EnsureHairCategory();
        Reporter.Log("Grid filtered to hair wearables");

        // Pick a hair that is not currently equipped so the test is re-runnable.
        var target = Views.ExplorePanel.Backpack.Wearables.FindUnequippedGridItem();
        EquipUntilShown(target);

        Assert.That(target.IsEquipped(), Is.True,
            "Grid item should report equipped after pressing its Equip button");
        Reporter.Log("Wearable equipped from the grid tile");

        Views.ExplorePanel.Close();
    }

    /// <summary>
    /// Covers the client's own double-click equip (BackpackItemView maps clickCount == 2 to
    /// Equip). Separate from the button path because it exercises a different client entry
    /// point, not because the two should behave differently.
    /// </summary>
    [Test]
    public void TestDoubleClickEquipWearable()
    {
        // The feature works for a human; the driver cannot deliver it dependably. Unity only
        // raises clickCount == 2 when both presses resolve to the same handler inside its
        // window, and AltTester queues the pointer move with the first press, so the hover
        // overlay re-animates underneath it. Roughly half the recorded attempts equipped.
        // Everything tried — count 2 vs 4, intervals, cursor parking, settling — moved the
        // rate but never fixed it, so this is gated rather than left flaking.
        if (OperatingSystem.IsMacOS())
            Assert.Ignore("pending a reliable synthetic double-click: clickCount == 2 reaches the client on roughly half of attempts, the rest register as a selection");

        OpenWearables();

        Views.ExplorePanel.Backpack.Wearables.EnsureHairCategory();
        Reporter.Log("Grid filtered to hair wearables");

        var target = Views.ExplorePanel.Backpack.Wearables.FindUnequippedGridItem();
        target.DoubleClickEquip();
        WaitUntil(target.ReadEquippedFlag, EQUIP_CONFIRM_TIMEOUT);

        Assert.That(target.IsEquipped(), Is.True,
            "Grid item should report equipped after a double-click");
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
    /// Presses the item's Equip button and waits for the client's equipped flag. Not retried:
    /// the button either reaches OnEquip or the overlay was not up, and re-pressing does not
    /// change that. Polls the flag rather than IsEquipped so the loop does not re-hover; the
    /// caller's assertion does that once.
    /// </summary>
    private void EquipUntilShown(ExplorePanelBackpackView.BackpackGridItem item)
    {
        item.Equip();
        WaitUntil(item.ReadEquippedFlag, EQUIP_CONFIRM_TIMEOUT);
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
    /// Reads the leading item's name until two consecutive reads agree, so a baseline is
    /// pinned to a grid that has stopped moving. FirstLoadedGridItem is an unindexed path,
    /// and the grid pool keeps stale tiles enabled while results stream in, so a single read
    /// can land on a tile the page is about to replace — the defect that failed the emotes
    /// pagination round-trip on CI run 31164127596 and is latent here.
    /// Selection stays on FirstLoadedGridItem because that is the variant with a green run
    /// behind it. What CI actually disproved was clicking the tile ROOT (<c>GridItems[i]</c>),
    /// which left the info panel unpopulated on run 31176916555. An indexed FullBackpack path
    /// would be both deterministic and selectable and is the obvious next thing to try, but it
    /// has never been run — do not assume it is ruled out.
    /// Reads without a <c>previousName</c> on purpose: this needs two AGREEING samples, so
    /// PR #54's "text differs from the previous name" predicate would invert the very
    /// condition being established.
    /// </summary>
    private string ReadSettledFirstItemName(ExplorePanelBackpackView.WearablesTab wearables)
    {
        wearables.FirstLoadedGridItem.WaitUntilLoaded(SlowChassis.SETTLE_TIMEOUT);
        var name = SelectItemAndReadName(wearables.FirstLoadedGridItem, wearables.SelectedItemName);

        for (var attempt = 0; attempt < SlowChassis.SETTLE_READS; attempt++)
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

        for (var attempt = 0; attempt < PAGE_FLIP_ATTEMPTS; attempt++)
        {
            // Paravirt ceiling on the load wait (ours), previousName on the read (PR #54):
            // this read only has to notice the flip, so polling for a differing name is right.
            wearables.FirstLoadedGridItem.WaitUntilLoaded(SlowChassis.SETTLE_TIMEOUT);
            var name = SelectItemAndReadName(wearables.FirstLoadedGridItem, wearables.SelectedItemName, previousName);
            if (name != previousName)
                // Settle before returning, do not trust the first differing read. This is the
                // value the round-trip assertion compares, so it is the read that has to be
                // stable: a grid caught mid-re-bind can briefly show neither the outgoing nor
                // the settled incoming page. Settling only the page-1 baseline would leave the
                // failing read unguarded.
                return ReadSettledFirstItemName(wearables);

            Wait(2);
        }

        // Still reading the outgoing page's name: return a settled read so the caller's
        // assertion fails against what the grid actually ended up showing.
        return ReadSettledFirstItemName(wearables);
    }
}
