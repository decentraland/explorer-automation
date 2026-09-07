using ExplorerAutomation.Tests.Views.ExplorePanelSections;

namespace ExplorerAutomation.Tests.Tests;

[AllureSuite("Backpack Emotes Tests")]
[Category("InWorld")]
[Order(10)]
public class BackpackEmotesTests : BaseTest
{
    // Per-equip budget to retry a dropped click, not to outwait a slow client.
    private const double EQUIP_SETTLE_PER_ATTEMPT = 8;
    private const int PAGE_FLIP_ATTEMPTS = 3;
    // A swallowed click is only undone by another click, so poll briefly and click again.
    private const double RETRY_READ_TIMEOUT = 4;
    // Interval between the two samples that pin a read; the catalogue re-sorts tens of seconds
    // after the page reports full.
    private const double SETTLE_SAMPLE_INTERVAL = 5;
    private const int CATALOG_SETTLE_READS = 7;

    [Test]
    public void TestUnequipAndEquipAllEmoteSlots()
    {
        OpenEmotes();

        var emotes = Views.ExplorePanel.Backpack.Emotes;
        emotes.UnequipAll();

        for (var i = 0; i < ExplorePanelBackpackView.EmotesTab.SLOT_COUNT; i++)
        {
            // Retry on the target SLOT filling: a dropped slot click equips into the
            // previously selected slot, badging the tile while leaving slot i empty.
            var index = i;
            ClickUntil(() => emotes.SetEmote(index, index),
                       () => !emotes.Slots[index].EmptyNameLabel.IsPresent(verificationShot: false),
                       timeoutPerAttempt: EQUIP_SETTLE_PER_ATTEMPT);
        }

        // Only the final equip's badge can still be propagating; the earlier ones were
        // confirmed slots ago, so assert those instead of paying a ceiling per item.
        emotes.GridItems[ExplorePanelBackpackView.EmotesTab.SLOT_COUNT - 1]
              .EquippedSlotBadge.WaitFor(SlowChassis.SETTLE_TIMEOUT);

        for (var i = 0; i < ExplorePanelBackpackView.EmotesTab.SLOT_COUNT; i++)
        {
            // Every badge must still be lit after the last equip: filling a later slot
            // must not evict an emote already assigned to an earlier one.
            Assert.That(emotes.GridItems[i].EquippedSlotBadge.IsPresent(verificationShot: false), Is.True,
                $"Emote {i} should still be equipped after all ten slots are filled");
        }

        Reporter.Log("All emote slots equipped sequentially and badges verified");

        Views.ExplorePanel.Close();
    }

    [Test]
    public void TestSearchAndEquipFistPump()
    {
        OpenEmotes();

        var emotes = Views.ExplorePanel.Backpack.Emotes;

        Views.ExplorePanel.Backpack.SearchBar.SetText("Fist Pump");

        // The grid keeps its pre-search tiles until the search debounce elapses, so the page
        // going short is the observable moment the results landed.
        Assert.That(WaitUntil(() => !emotes.HasFullGridPage(), timeoutSeconds: SlowChassis.SETTLE_TIMEOUT),
            Is.True, "Emote grid should refilter to the 'Fist Pump' search results");

        emotes.UnequipAll();

        // The leading LOADED tile, not GridItems[0]: on a one-result page index 0 is a blank cell.
        ClickUntil(() => emotes.SetFirstLoadedEmote(0),
                   () => !emotes.Slots[0].EmptyNameLabel.IsPresent(verificationShot: false),
                   timeoutPerAttempt: EQUIP_SETTLE_PER_ATTEMPT);

        // Assert the slot filled before reading its label. EmoteName only exists once an emote
        // occupies the slot, so reading first reports a failed equip as a missing object and
        // spends the label's own ceiling getting there.
        Assert.That(emotes.Slots[0].EmptyNameLabel.IsPresent(), Is.False,
            "Slot 0 should hold an emote after the equip, but it is still empty");

        // The slot label names the emote that landed, so it proves the searched item reached the
        // slot asked for — not merely that something is equipped.
        var slotName = emotes.Slots[0].NameLabel.WaitForText(
            text => !string.IsNullOrEmpty(text) && text.Contains("Fist Pump", StringComparison.OrdinalIgnoreCase),
            timeoutSeconds: SlowChassis.SETTLE_TIMEOUT);
        Assert.That(slotName, Does.Contain("Fist Pump").IgnoreCase,
            $"Slot 0 should hold the searched emote after equipping it, but its label reads '{slotName}'");
        Reporter.Log("Fist Pump equipped to slot 0 and confirmed on the slot label");

        Views.ExplorePanel.Close();
    }

    [Test]
    public void TestUnequipEmoteWithSlotButton()
    {
        OpenEmotes();

        var emotes = Views.ExplorePanel.Backpack.Emotes;

        // Equip a known emote to slot 5 first so the unequip has a deterministic target.
        var gridIndex = emotes.FindUnequippedGridItemIndex();
        ClickUntil(() => emotes.SetEmote(4, gridIndex),
                   () => emotes.GridItems[gridIndex].EquippedSlotBadge.IsPresent(verificationShot: false),
                   timeoutPerAttempt: EQUIP_SETTLE_PER_ATTEMPT);

        // ClickUntil already polled this for the whole budget, so assert rather than wait again.
        Assert.That(emotes.GridItems[gridIndex].EquippedSlotBadge.IsPresent(), Is.True,
            $"Precondition: grid item {gridIndex} should be equipped before the unequip is exercised");
        Reporter.Log($"Precondition ready — grid item {gridIndex} equipped to slot 5");

        // The slot's own button — the grid item's hover Unequip ignores synthetic input.
        emotes.ClickSlot(4);
        emotes.ClickUnequip(4);

        emotes.GridItems[gridIndex].EquippedSlotBadge.WaitForGone(SlowChassis.SETTLE_TIMEOUT);
        emotes.Slots[4].EmptyNameLabel.WaitFor(SlowChassis.SETTLE_TIMEOUT);
        Reporter.Log("Emote unequipped via the slot's explicit Unequip button — slot 5 is empty");

        Views.ExplorePanel.Close();
    }

    [Test]
    public void TestEmotesPagination()
    {
        OpenEmotes();

        var emotes = Views.ExplorePanel.Backpack.Emotes;
        emotes.Pager.WaitFor();

        // One anchor for the whole test: the grid layout holds its screen position across
        // pages, so every read names THAT cell. Re-picking the leading loaded tile walks to
        // the neighbour, because the tile just clicked drops out of the loaded set.
        var cell = emotes.FindTopLeftLoadedTile(SlowChassis.SETTLE_TIMEOUT);

        var firstPageItem = ReadSettledFirstItemName(emotes, cell);
        Reporter.Log($"Page 1 first emote: {firstPageItem}");
        Reporter.TakeScreenshot("emotes_read_page1_baseline");

        var secondPageItem = FlipPageAndReadFirstItem(emotes, emotes.Pager.NextButton, firstPageItem, cell);
        Reporter.Log($"Page 2 first emote: {secondPageItem}");
        Reporter.TakeScreenshot("emotes_read_page2");
        Assert.That(secondPageItem, Is.Not.EqualTo(firstPageItem),
            "First emote on page 2 should differ from first emote on page 1");

        var backOnFirstPage = FlipPageAndReadFirstItem(emotes, emotes.Pager.PreviousButton, secondPageItem, cell);
        Reporter.TakeScreenshot("emotes_read_page1_again");
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

        // A search term from an earlier test outlives the panel and shrinks the page, which
        // index addressing cannot survive — clear it (after the tab click, since only the active
        // section's grid picks it up) and wait out the rebuild it triggers.
        Views.ExplorePanel.Backpack.ClearSearch();
        Views.ExplorePanel.Backpack.Emotes.WaitForGridPageLoaded();
    }

    /// <summary>
    /// Selects the grid's top-left loaded item and reads its name from the info panel, polling
    /// the label until it shows text (differing from <paramref name="previousName"/> when given).
    /// Picks by screen position, since the pool does not keep siblings in display order.
    /// </summary>
    private string ReadFirstItemName(
        ExplorePanelBackpackView.EmotesTab emotes,
        AltObject cell,
        string previousName = null,
        double timeoutSeconds = 10)
    {
        // Anchored to the cell's screen position: the unindexed path answers with the page's
        // trailing cell, whose content shifts when a late arrival moves the collection.
        emotes.FindTopLeftLoadedTile(SlowChassis.SETTLE_TIMEOUT, anchor: cell).Click();
        return emotes.SelectedItemName.WaitForText(
            text => !string.IsNullOrEmpty(text) && text != previousName, timeoutSeconds);
    }

    /// <summary>
    /// Reads the leading item's name until two consecutive reads agree, so the baseline is
    /// pinned to a grid that has stopped moving rather than captured mid-stream. Throws when
    /// they never agree — a value known to be moving is not a baseline.
    /// </summary>
    private string ReadSettledFirstItemName(ExplorePanelBackpackView.EmotesTab emotes, AltObject cell)
    {
        var name = ReadFirstItemName(emotes, cell);

        for (var attempt = 0; attempt < CATALOG_SETTLE_READS; attempt++)
        {
            // The interval between two samples is the measurement, not padding around one.
            Wait(SETTLE_SAMPLE_INTERVAL);
            var reread = ReadFirstItemName(emotes, cell);
            if (reread == name)
                return name;

            name = reread;
        }

        throw new AssertionException(
            $"The emote grid never stopped changing: {CATALOG_SETTLE_READS + 1} reads taken "
            + $"{SETTLE_SAMPLE_INTERVAL}s apart each named a different item, the last '{name}'. "
            + "The catalogue is still arriving and re-sorting the page.");
    }

    /// <summary>
    /// Clicks a pager arrow and reads the re-selected first item name, re-clicking while the
    /// name has not changed — clicks during the grid rebuild are no-ops that leave a stale name.
    /// </summary>
    private string FlipPageAndReadFirstItem(
        ExplorePanelBackpackView.EmotesTab emotes,
        Clickable pagerButton,
        string previousName,
        AltObject cell)
    {
        pagerButton.Click();

        for (var attempt = 0; attempt < PAGE_FLIP_ATTEMPTS; attempt++)
        {
            var name = ReadFirstItemName(emotes, cell, previousName, RETRY_READ_TIMEOUT);
            if (name != previousName)
                // Settle before returning: this is the value the round-trip assertion compares,
                // and a grid caught mid-re-bind can briefly show neither page.
                return ReadSettledFirstItemName(emotes, cell);
        }

        // Still on the outgoing page's name: return a settled read so the caller's assertion
        // fails against what the grid actually ended up showing.
        return ReadSettledFirstItemName(emotes, cell);
    }
}
