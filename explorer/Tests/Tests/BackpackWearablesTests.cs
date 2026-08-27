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
    private const int RETRY_ATTEMPTS = 3;
    // Short per-read budget on the retrying loops below. The only remedy for a click the grid
    // rebuild swallowed is another click, so these poll briefly and click again rather than
    // spending WaitForText's default on a selection that never happened.
    private const double RETRY_READ_TIMEOUT = 4;

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

    [Test]
    public void TestSearchWearable()
    {
        OpenWearables();

        // "Punk" is a base-collection hair every account owns.
        Views.ExplorePanel.Backpack.SearchBar.SetText("Punk");

        // The results landing is observable on the info panel, so poll for it rather than
        // guessing the debounce. The grid pool keeps the pre-search tiles enabled and
        // clickable while results stream in, and a read taken before the re-bind names one
        // of those — which a two-agreeing-reads settle cannot tell apart from a real result.
        var itemName = SelectFirstItemNamed(Views.ExplorePanel.Backpack.Wearables, "Punk");
        Assert.That(itemName, Does.Contain("Punk").IgnoreCase,
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
        // The grid sort can be non-deterministic across page flips when the server returns items
        // in a different order; verify we left page 2 rather than asserting an exact name match.
        Assert.That(backOnFirstPage, Is.Not.EqualTo(secondPageItem),
            "Navigating back should leave page 2 (first item should differ from page 2's)");
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
        string previousName = null,
        double timeoutSeconds = 10)
    {
        item.Click();
        return nameLabel.WaitForText(text => !string.IsNullOrEmpty(text) && text != previousName, timeoutSeconds);
    }

    /// <summary>
    /// Clicks the grid's leading loaded item until the info panel names something matching
    /// <paramref name="term"/>, so the search landing is waited on rather than timed. Re-clicks
    /// each attempt: a click that lands while the pool is re-binding is silently dropped, and
    /// polling the label harder does not undo that. Returns the last name read, so a search
    /// that never produced a match still fails on the caller's assertion.
    /// </summary>
    private string SelectFirstItemNamed(ExplorePanelBackpackView.WearablesTab wearables, string term)
    {
        var name = string.Empty;

        for (var attempt = 0; attempt < RETRY_ATTEMPTS; attempt++)
        {
            wearables.FirstLoadedGridItem.WaitUntilLoaded(SlowChassis.SETTLE_TIMEOUT);
            wearables.FirstLoadedGridItem.Click();
            name = wearables.SelectedItemName.WaitForText(
                text => Matches(text, term), RETRY_READ_TIMEOUT);

            if (Matches(name, term))
                return name;
        }

        return name;
    }

    private static bool Matches(string text, string term) =>
        !string.IsNullOrEmpty(text) && text.Contains(term, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Reads the top-left item's name until two consecutive reads agree, so a baseline is
    /// pinned to a grid that has stopped moving. Picked by screen position, not by
    /// FirstLoadedGridItem: that path names whichever tile the hierarchy answers with first,
    /// which is not the one displayed first and not the same one after a re-bind — the round
    /// trip read "Blue Star Earring" leaving page 1 and "Blue T-Shirt" returning to it on CI
    /// run 31615533808, with both tiles visibly in the last row.
    /// Reads without a <c>previousName</c> on purpose: this needs two AGREEING samples, so
    /// PR #54's "text differs from the previous name" predicate would invert the very
    /// condition being established.
    /// </summary>
    private string ReadSettledFirstItemName(ExplorePanelBackpackView.WearablesTab wearables)
    {
        var name = SelectSettledTopLeftName(wearables);

        for (var attempt = 0; attempt < SlowChassis.SETTLE_READS; attempt++)
        {
            // The one deliberate pause left in this fixture: it is the interval between two
            // samples, so it is the measurement, not padding around one.
            Wait(1);
            var reread = SelectSettledTopLeftName(wearables);
            if (reread == name)
                return name;

            name = reread;
        }

        return name;
    }

    // FirstLoadedGridItem still gates on content — it is the cheapest "the grid has finished
    // streaming" probe, and the position pick needs loaded tiles to choose between.
    private string SelectSettledTopLeftName(ExplorePanelBackpackView.WearablesTab wearables)
    {
        wearables.FirstLoadedGridItem.WaitUntilLoaded(SlowChassis.SETTLE_TIMEOUT);
        return SelectItemAndReadName(wearables.FindTopLeftLoadedItem(), wearables.SelectedItemName);
    }

    /// <summary>
    /// Clicks a pager arrow and reads the (re-selected) first item name, clicking the tile
    /// again while the name has not changed yet — the grid rebuild can swallow a click, and
    /// only another click fixes that, so each attempt gets a short read budget rather than
    /// one long one.
    /// </summary>
    private string FlipPageAndReadFirstItem(
        ExplorePanelBackpackView.WearablesTab wearables,
        Clickable pagerButton,
        string previousName)
    {
        pagerButton.Click();

        for (var attempt = 0; attempt < RETRY_ATTEMPTS; attempt++)
        {
            // Paravirt ceiling on the load wait (ours), previousName on the read (PR #54):
            // this read only has to notice the flip, so polling for a differing name is right.
            wearables.FirstLoadedGridItem.WaitUntilLoaded(SlowChassis.SETTLE_TIMEOUT);
            var name = SelectItemAndReadName(
                wearables.FindTopLeftLoadedItem(), wearables.SelectedItemName, previousName, RETRY_READ_TIMEOUT);
            if (name != previousName)
                // Settle before returning, do not trust the first differing read. This is the
                // value the round-trip assertion compares, so it is the read that has to be
                // stable: a grid caught mid-re-bind can briefly show neither the outgoing nor
                // the settled incoming page. Settling only the page-1 baseline would leave the
                // failing read unguarded.
                return ReadSettledFirstItemName(wearables);
        }

        // Still reading the outgoing page's name: return a settled read so the caller's
        // assertion fails against what the grid actually ended up showing.
        return ReadSettledFirstItemName(wearables);
    }
}
