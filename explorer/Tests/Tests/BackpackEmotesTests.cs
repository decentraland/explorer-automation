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

    // Wall-clock budget per equip attempt. A landed equip fills the slot in a second or two
    // and the measured confirm is ~3s, so this budget exists to retry a dropped click, not to
    // outwait a slow client. Matches BackpackWearablesTests now that both fixtures confirm
    // with a cheap read. Re-equipping is idempotent (double-click maps to Equip, not to a
    // toggle), so an extra attempt against an already-equipped item is harmless.
    private const double EQUIP_SETTLE_PER_ATTEMPT = 8;
    private const int PAGE_FLIP_ATTEMPTS = 3;
    // Short per-read budget on the page-flip loop. The only remedy for a click the grid rebuild
    // swallowed is another click, so it polls briefly and clicks again rather than spending
    // WaitForText's default on a selection that never happened.
    private const double RETRY_READ_TIMEOUT = 4;
    // Interval between the two samples that pin a read. One second only ever caught a grid
    // moving within a frame of itself; the reflow that breaks this test lands tens of seconds
    // after WaitForGridPageLoaded returns, because that wait sees sixteen bound tiles and
    // cannot see a catalogue still arriving behind them.
    private const double SETTLE_SAMPLE_INTERVAL = 5;

    [Test]
    public void TestUnequipAndEquipAllEmoteSlots()
    {
        // Ten sequential equips — by far the heaviest thing this fixture does. It was gated on
        // one grid item losing its badge, a different one each run, diagnosed as the grid
        // displacing an earlier emote. That was measured when equipping was a double-click
        // that landed about half the time, and ten in a row cannot all survive those odds, so
        // the diagnosis never had a reliable equip under it. If a badge still goes missing now
        // that equipping presses the Equip button, the cause is grid/slot behaviour and
        // belongs in a client bug rather than in a wider wait here.
        OpenEmotes();

        var emotes = Views.ExplorePanel.Backpack.Emotes;
        emotes.UnequipAll();

        for (var i = 0; i < ExplorePanelBackpackView.EmotesTab.SLOT_COUNT; i++)
        {
            // Retry on the target SLOT filling, not on the grid item's badge appearing.
            // SetEmote selects the slot and then equips into whatever slot is selected, so
            // when the slot click is the part that gets dropped the emote lands in the
            // previously selected slot instead — badging the grid item while leaving slot i
            // empty and silently displacing an earlier emote. Slot occupancy is the only
            // condition that distinguishes "equipped where we asked" from "equipped".
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

        emotes.UnequipEmoteIfPresent(0);

        // The leading LOADED tile, not GridItems[0]: on a one-result page index 0 is a blank cell
        // and clicks on it are no-ops. The equip double-click is droppable, so confirm the slot
        // filled before asserting.
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
        // See TestUnequipAndEquipAllEmoteSlots — the equip double-click is droppable, so
        // confirm it landed before treating the precondition as ready.
        ClickUntil(() => emotes.SetEmote(4, gridIndex),
                   () => emotes.GridItems[gridIndex].EquippedSlotBadge.IsPresent(verificationShot: false),
                   timeoutPerAttempt: EQUIP_SETTLE_PER_ATTEMPT);

        // Assert rather than wait again: ClickUntil just polled this exact condition for the
        // whole retry budget, so a second wait only adds its own ceiling to a decided failure.
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

        // A shot per read, unconditionally — this test's failures are all about what the grid
        // held at a given moment, and the teardown's final frame only ever shows the last one.
        // Verification shots would not do: CI leaves them off by default, so the runs worth
        // examining are exactly the ones that would carry none.
        var firstPageItem = ReadSettledFirstItemName(emotes);
        Reporter.Log($"Page 1 first emote: {firstPageItem}");
        Reporter.TakeScreenshot("emotes_read_page1_baseline");

        var secondPageItem = FlipPageAndReadFirstItem(emotes, emotes.Pager.NextButton, firstPageItem);
        Reporter.Log($"Page 2 first emote: {secondPageItem}");
        Reporter.TakeScreenshot("emotes_read_page2");
        Assert.That(secondPageItem, Is.Not.EqualTo(firstPageItem),
            "First emote on page 2 should differ from first emote on page 1");

        var backOnFirstPage = FlipPageAndReadFirstItem(emotes, emotes.Pager.PreviousButton, secondPageItem);
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

        // Order matters. A search term from an earlier test outlives the panel and shrinks the
        // page, which index addressing cannot survive, so clear it first — after the tab click,
        // because clearing only reaches the active section's grid. Then wait out the rebuild
        // that the clear triggers. Both unconditional: gating the clear on a full page asked
        // one tile whether sixteen were ready, and every caller here addresses tiles by index.
        Views.ExplorePanel.Backpack.ClearSearch();
        Views.ExplorePanel.Backpack.Emotes.WaitForGridPageLoaded();
    }

    /// <summary>
    /// Selects the grid's top-left loaded item and reads its name from the info panel, polling
    /// the label (PR #54's <c>WaitForText</c>) until it shows text — and, when the caller
    /// supplies <paramref name="previousName"/>, text that differs from it — instead of
    /// reading after a fixed settle.
    /// Picks by screen position. An indexed path would not help: the index selects a sibling,
    /// and the pool does not keep siblings in display order. Clicking the tile ROOT
    /// (<c>GridItems[i]</c>) is separately ruled out — it left the info panel unpopulated on
    /// run 31176916555, so the click goes to the FullBackpack child either way.
    /// </summary>
    private string ReadFirstItemName(
        ExplorePanelBackpackView.EmotesTab emotes,
        string previousName = null,
        double timeoutSeconds = 10)
    {
        // Screen position, not hierarchy order: the unindexed path answers with the page's
        // trailing cell, the one cell whose content changes when a late arrival shifts the
        // collection by one. Run 31791128888 read "Robot" and then "Ho Ho Ho" off that cell.
        // Still one lookup — the sweep returns the object to click, nothing to re-resolve.
        return ReadTileName(emotes, emotes.FindTopLeftLoadedTile(SlowChassis.SETTLE_TIMEOUT),
                            previousName, timeoutSeconds);
    }

    /// <summary>
    /// Selects one tile and reads the name the info panel gives it.
    /// </summary>
    private string ReadTileName(
        ExplorePanelBackpackView.EmotesTab emotes,
        AltObject tile,
        string previousName = null,
        double timeoutSeconds = 10)
    {
        tile.Click();
        return emotes.SelectedItemName.WaitForText(
            text => !string.IsNullOrEmpty(text) && text != previousName, timeoutSeconds);
    }

    /// <summary>
    /// Reads the leading item's name until two consecutive reads agree, so the page-1
    /// baseline is pinned to a grid that has stopped moving. Without this the baseline can
    /// be captured mid-stream and the round-trip assertion compares two different pages'
    /// contents (CI run 31164127596 read "Head Explode" out and "Ho Ho Ho" back).
    /// Reads without a <c>previousName</c> on purpose: this needs two AGREEING samples, so
    /// PR #54's "text differs from the previous name" predicate would invert the very
    /// condition being established.
    /// Throws when they never agree — a value known to be moving is not a baseline.
    /// </summary>
    private string ReadSettledFirstItemName(ExplorePanelBackpackView.EmotesTab emotes)
    {
        var cell = emotes.FindTopLeftLoadedTile(SlowChassis.SETTLE_TIMEOUT);
        var name = ReadTileName(emotes, cell);

        for (var attempt = 0; attempt < SlowChassis.SETTLE_READS; attempt++)
        {
            // The one deliberate pause left in this fixture: it is the interval between two
            // samples, so it is the measurement, not padding around one.
            Wait(SETTLE_SAMPLE_INTERVAL);

            // Anchored to the cell the first sample landed on. Clicking a tile drops it out of
            // the loaded set until its preview finishes rendering, so a fresh pick walks to the
            // neighbouring cell and the samples alternate forever (CI run 32152404832).
            cell = emotes.FindTopLeftLoadedTile(SlowChassis.SETTLE_TIMEOUT, anchor: cell);
            var reread = ReadTileName(emotes, cell);
            if (reread == name)
                return name;

            name = reread;
        }

        // Returning the last read is what let a still-moving grid reach the round-trip
        // assertion and fail there as a pagination bug. This is the only place that asserts
        // the collection stopped changing, so it has to fail here instead.
        throw new AssertionException(
            $"The emote grid never stopped changing: {SlowChassis.SETTLE_READS + 1} reads taken "
            + $"{SETTLE_SAMPLE_INTERVAL}s apart each named a different item, the last '{name}'. "
            + "The catalogue is still arriving and re-sorting the page.");
    }

    /// <summary>
    /// Clicks a pager arrow and reads the (re-selected) first item name, clicking the tile
    /// again while the name has not changed yet — clicks during the grid rebuild are no-ops
    /// and leave a stale name in the info panel, so each attempt gets a short read budget
    /// rather than one long one.
    /// </summary>
    private string FlipPageAndReadFirstItem(
        ExplorePanelBackpackView.EmotesTab emotes,
        Clickable pagerButton,
        string previousName)
    {
        pagerButton.Click();

        for (var attempt = 0; attempt < PAGE_FLIP_ATTEMPTS; attempt++)
        {
            // previousName here (PR #54): this read only has to notice the flip, so polling
            // for a differing name is right. No separate WaitUntilLoaded — ReadFirstItemName
            // already waits on the paravirt ceiling, the stronger of the two.
            var name = ReadFirstItemName(emotes, previousName, RETRY_READ_TIMEOUT);
            if (name != previousName)
                // Settle before returning, do not trust the first differing read. This is the
                // value the round-trip assertion compares, so it is the read that has to be
                // stable: a grid caught mid-re-bind can briefly show neither the outgoing nor
                // the settled incoming page. Settling only the page-1 baseline would leave the
                // failing read unguarded.
                return ReadSettledFirstItemName(emotes);
        }

        // Still reading the outgoing page's name: return a settled read so the caller's
        // assertion fails against what the grid actually ended up showing.
        return ReadSettledFirstItemName(emotes);
    }
}
