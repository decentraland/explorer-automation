namespace ExplorerAutomation.Tests.Views.ExplorePanelSections;

/// <summary>
/// Section view for the Backpack tab within the explore panel, where users manage
/// their equipped wearables, emotes and saved outfits.
/// </summary>
public class ExplorePanelBackpackView() : BaseSection(new(By.NAME, "BackpackSection"))
{
    #region Elements

    // Wait ceilings for this panel, sized from what CI actually spends rather than from the
    // repo-wide SlowChassis ceiling. Measured on the paravirt chassis: a static element
    // appears in under 0.7s, a hover overlay in under 0.5s, streamed content (grid tiles,
    // outfit slots, info-panel text) in under 2.5s with one 8.2s outlier on a cold grid, and
    // a whole grid page in 5.4s. Every wait below takes one of these, so a chassis that
    // outgrows them is retuned in one place.
    private const double UI_TIMEOUT      = 5D;   // the element is either there or it is not
    private const double CONTENT_TIMEOUT = 15D;  // waiting on content the client streams in
    // Deliberately not lowered with the rest: the equip double-click is sensitive to how long
    // the tile has been hovered, and this is the one ceiling that could affect it.
    private const double OVERLAY_SETTLE  = 2D;   // hover overlay animating in
    private const int OVERLAY_POLL_MS    = 100;
    private const int TAB_SWITCH_MS      = 500;  // between sub-tab toggle retries
    private const int PRE_EQUIP_SETTLE_MS = 500;

    // Main tabs (Header/TabSelector) — Wearables ("Avatar") and Emotes toggles.
    public readonly Clickable WearablesTabButton    = new(By.PATH, "//TabSelector/Avatar");
    public readonly Clickable EmotesTabButton       = new(By.PATH, "//TabSelector/Emotes");
    // Sub-tabs of the wearables view (HeaderContainer/TabSelector) — Categories and Saved Outfits.
    public readonly Clickable CategoriesTabButton   = new(By.PATH, "//TabSelector/ToggleCategories");
    public readonly Clickable SavedOutfitsTabButton = new(By.PATH, "//TabSelector/ToggleOutfits");
    public readonly Writable  SearchBar             = new(By.PATH, "//BackpackSection//SearchBar");

    #endregion

    #region Views

    public WearablesTab    Wearables    { get; } = new();
    public EmotesTab       Emotes       { get; } = new();
    public SavedOutfitsTab SavedOutfits { get; } = new();

    #endregion

    #region Helper methods

    /// <summary>
    /// Clicking the Categories/Saved Outfits toggles occasionally double-fires and leaves
    /// the previous sub-tab active, so both switch helpers verify and retry.
    /// </summary>
    [AllureStep("Open the Saved Outfits sub-tab")]
    public void OpenSavedOutfits()
    {
        SwitchSubTab(SavedOutfitsTabButton, SavedOutfits);
        Reporter.Log("Saved Outfits sub-tab open");
    }

    [AllureStep("Open the Categories sub-tab")]
    public void OpenCategories()
    {
        SwitchSubTab(CategoriesTabButton, Wearables);
        Reporter.Log("Categories sub-tab open");
    }

    private static void SwitchSubTab(Clickable tabButton, BaseView targetView)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            // Shot-suppressed probe — the WaitFor below takes the verification shot.
            if (targetView.IsPresent(verificationShot: false))
            {
                targetView.WaitFor(UI_TIMEOUT);
                return;
            }

            tabButton.Click();
            Thread.Sleep(TAB_SWITCH_MS);
        }

        // Final wait throws with a useful message if the tab still is not open.
        targetView.WaitFor(UI_TIMEOUT);
    }

    /// <summary>
    /// Empties the search bar. A search term outlives the panel closing, so clear it before
    /// anything that needs an unfiltered grid. Call with the target tab already open — only the
    /// active section's grid picks the change up.
    /// </summary>
    [AllureStep("Clear the backpack search bar")]
    public void ClearSearch()
    {
        SearchBar.SetText(string.Empty);
        Reporter.Log("Backpack search bar cleared");
    }

    #endregion

    #region Sub views

    /// <summary>
    /// A single item in a backpack grid (wearable or emote), with equip/unequip buttons
    /// that are only enabled while the item is hovered.
    /// </summary>
    public class BackpackGridItem(Clickable root, Clickable equipLocator, Clickable unequipLocator, Locatable loadedLocator)
        : BaseClickableView(root)
    {
        #region Elements

        // Equipped state comes off the view's own flag. Every rendered alternative is unusable:
        // the Equipped icon is never switched on by OnEquip (only by a re-bind or a pointer
        // exit), and the overlay's Unequip button needs a hover to be readable at all.
        // Emote tiles answer to the same component — BackpackEmoteGridItemView derives from it.
        private const string ITEM_VIEW_COMPONENT = "DCL.Backpack.BackpackItemView";
        private const string ITEM_VIEW_ASSEMBLY  = "Backpack";

        // NOTE: the hover overlay's Equip/Unequip Buttons do NOT respond to synthetic
        // AltTester clicks or taps in this build (verified live — the click lands but no
        // equip happens). Use DoubleClickEquip to actually equip.
        public Clickable EquipButton   { get; } = equipLocator;
        public Clickable UnequipButton { get; } = unequipLocator;
        // FullBackpack is only enabled once the tile has real content; while the tile is
        // still loading, EmptyLoading is shown instead and clicks on it are no-ops.
        public Locatable LoadedIndicator { get; } = loadedLocator;

        #endregion

        #region Helper methods

        [AllureStep("Wait for grid item content to load")]
        public void WaitUntilLoaded(double timeout = CONTENT_TIMEOUT)
        {
            LoadedIndicator.WaitFor(timeout);
        }

        /// <summary>
        /// Moves the pointer over the item so the HoverBackground overlay (with the
        /// Equip/Unequip buttons) becomes enabled. Hover state only survives within a
        /// single driver session, so callers must click in the same test step.
        /// </summary>
        [AllureStep("Hover grid item")]
        public AltObject Hover()
        {
            // Shot-suppressed wait: hovering is an action (like Click), not a verification.
            var altObj = WaitFor(UI_TIMEOUT, verificationShot: false);
            altObj.PointerEnter();

            // Wait for the hover overlay to actually enable instead of guessing a fixed delay:
            // exactly one of Equip/Unequip becomes present once the overlay finishes animating in.
            var deadline = DateTime.UtcNow.AddSeconds(OVERLAY_SETTLE);
            while (DateTime.UtcNow < deadline
                   && !EquipButton.IsPresent(verificationShot: false)
                   && !UnequipButton.IsPresent(verificationShot: false))
                Thread.Sleep(OVERLAY_POLL_MS);

            return altObj;
        }

        /// <summary>
        /// BackpackItemView.OnPointerClick treats clickCount == 2 as Equip, so two rapid
        /// clicks equip without needing the hover overlay.
        /// </summary>
        [AllureStep("Equip grid item via double-click")]
        public void DoubleClickEquip()
        {
            // Shot-suppressed wait: double-click equip is an action, not a verification.
            var altObj = WaitFor(UI_TIMEOUT, verificationShot: false);

            // Settle before clicking. Every CI equip that has ever worked had ~1.8s or more of
            // hovering behind it, and every one clicked within ~1s of the first PointerEnter
            // has failed. The mechanism is not known — the hover animation is only 0.1s — so
            // this restores the delay the old three-probe IsEquipped provided by accident.
            Thread.Sleep(PRE_EQUIP_SETTLE_MS);
            // One Player-side command with count: 2, NOT two driver round-trips. Unity only
            // raises clickCount == 2 when the second click lands inside its double-click
            // window; on the macos-14 paravirt runner a single driver round-trip already
            // exceeds that window, so two separate Click calls read as two single clicks and
            // never equip. The interval here is applied by the Player, not by the network.
            // Click more times than needed to ensure the click lands inside the window.
            altObj.Click(count: 4, interval: 0.05f);
            Reporter.Log("Double-clicked grid item to equip");
        }

        /// <summary>
        /// Reads BackpackItemView's own equipped flag — the one thing OnEquip actually writes.
        /// No hover, so it stays true whether the pointer is on the tile or not.
        /// </summary>
        [AllureStep("Read grid item equipped flag")]
        internal bool ReadEquippedFlag()
        {
            // A blank pooled tile still carries the component, so its flag reads false for the
            // wrong reason. Content is a precondition, not part of the answer.
            if (!LoadedIndicator.IsPresent(verificationShot: false))
                throw new AssertionException(
                    $"Grid item '{ShotName}' holds no content — its equipped state cannot be read.");

            // Shot-suppressed wait: reading a component is not a verification on its own.
            return WaitFor(UI_TIMEOUT, verificationShot: false)
                .GetComponentProperty<bool>(ITEM_VIEW_COMPONENT, "IsEquipped", ITEM_VIEW_ASSEMBLY);
        }

        /// <summary>
        /// Reads the equipped flag, then hovers so the overlay shows the matching Equip/Unequip
        /// affordance in the verification shot.
        /// </summary>
        public bool IsEquipped() => IsEquipped(verificationShot: true);

        // Shot-suppressed overload for probe loops (FindUnequippedGridItem) — the probe is
        // control flow, not a test verification, so it must not multiply attachments.
        [AllureStep("Check whether grid item is equipped")]
        internal bool IsEquipped(bool verificationShot)
        {
            var equipped = ReadEquippedFlag();

            // Hover for the picture, not for the answer: callers equip in the same driver
            // session while the pointer is still here.
            Hover();

            if (verificationShot)
                Reporter.TakeVerificationShot($"{(equipped ? "equipped" : "unequipped")}_{ShotName}");
            return equipped;
        }

        #endregion
    }

    /// <summary>
    /// The page selector (previous/next arrows and numbered page buttons) shown under a
    /// backpack grid when the inventory spans multiple pages.
    /// </summary>
    public class PageSelector(string containerPath) : BaseView(new(By.PATH, containerPath))
    {
        #region Elements

        public readonly Clickable PreviousButton = new(By.PATH, $"{containerPath}/PreviousButton");
        public readonly Clickable NextButton     = new(By.PATH, $"{containerPath}/NextButton");

        #endregion
    }

    /// <summary>
    /// Sub-view for the wearables Categories view within the backpack: avatar category
    /// slots on the left, a paged grid of owned wearables, and an item info panel.
    /// </summary>
    public class WearablesTab : BaseView
    {
        #region Elements

        public const int GRID_ITEM_COUNT = 16;
        private const string GRID_PATH = "//Avatar/CategoriesView/FullContainer/BackpackGrid";

        public BackpackGridItem[] GridItems { get; }

        public readonly Clickable AvatarSlotHair = new(By.PATH, "//CategoriesView/SlotsContainer/AvatarSlotHair");
        // AvatarSlotView.SelectedBackground: enabled only while this slot's category is the
        // active filter. A direct child, not "//Background" — deeper nodes share the name and
        // are always enabled.
        public readonly Locatable AvatarSlotHairSelected = new(By.PATH, "//CategoriesView/SlotsContainer/AvatarSlotHair/Background");
        public readonly Readable  SelectedItemName       = new(By.PATH, "//Avatar/CategoriesView/FullContainer/ItemInfoPanel//ItemName");

        #endregion

        #region Setup

        public WearablesTab() : base(new(By.PATH, "//BackpackSection//Avatar/CategoriesView"))
        {
            GridItems = new BackpackGridItem[GRID_ITEM_COUNT];
            for (var i = 0; i < GRID_ITEM_COUNT; i++)
                GridItems[i] = BuildGridItem($"{GRID_PATH}/BackpackItem(Clone)[{i}]");

            // Rooted on the enabled FullBackpack child rather than the tile itself: the
            // grid pool keeps stale tiles enabled (clipped by the mask) after a search or
            // on partial pages, and only tiles with real content have FullBackpack active.
            var loadedPath = $"{GRID_PATH}/BackpackItem(Clone)/FullBackpack";
            FirstLoadedGridItem = new BackpackGridItem(
                new(By.PATH, loadedPath),
                new(By.PATH, $"{loadedPath}/HoverBackground/Equip"),
                new(By.PATH, $"{loadedPath}/HoverBackground/Unequip"),
                new(By.PATH, loadedPath));
        }

        private static BackpackGridItem BuildGridItem(string basePath) => new(
            new(By.PATH, basePath),
            new(By.PATH, $"{basePath}/FullBackpack/HoverBackground/Equip"),
            new(By.PATH, $"{basePath}/FullBackpack/HoverBackground/Unequip"),
            new(By.PATH, $"{basePath}/FullBackpack"));

        #endregion

        #region Views

        /// <summary>
        /// The first grid item that actually has loaded content — safe to click after a
        /// search or page change while pooled stale tiles are still enabled.
        /// </summary>
        public BackpackGridItem FirstLoadedGridItem { get; }

        public PageSelector Pager { get; } = new("//Avatar/CategoriesView/FullContainer/PageSelector");

        #endregion

        #region Helper methods

        /// <summary>
        /// Applies the Hair category filter idempotently and leaves the grid loaded. The slot
        /// is a toggle, so clicking one that is already selected would clear the filter.
        /// </summary>
        [AllureStep("Ensure the Hair category filter is applied")]
        public void EnsureHairCategory()
        {
            // Shot-suppressed probe — the shot below records the state the helper verified.
            if (!AvatarSlotHairSelected.IsPresent(verificationShot: false))
            {
                AvatarSlotHair.Click();
                AvatarSlotHairSelected.WaitFor(UI_TIMEOUT, verificationShot: false);

                // The filter command goes out with the selected background, but the grid only
                // blanks on a later frame; yield one so the wait below cannot be satisfied by
                // the outgoing category's still-loaded tiles.
                WaitOneFrame();
            }

            // The filter surviving from an earlier test says nothing about the grid, which
            // reloads whenever the panel is reopened — so wait on both paths.
            WaitForGridPageLoaded(verificationShot: false);
            Reporter.Log("Hair category filter is applied");
            Reporter.TakeVerificationShot("applied_HairCategoryFilter");
        }

        /// <summary>
        /// Waits until every tile on the page holds content. Only valid on a full page: a
        /// category with fewer owned items than <see cref="GRID_ITEM_COUNT"/> leaves the
        /// surplus tiles blank and this waits them out to the ceiling.
        /// </summary>
        public void WaitForGridPageLoaded() => WaitForGridPageLoaded(verificationShot: true);

        // Shot-suppressed overload for callers that already capture their own verified state.
        [AllureStep("Wait for the whole wearables grid page to load")]
        internal void WaitForGridPageLoaded(bool verificationShot)
        {
            // One budget shared across the tiles, not one ceiling each — a grid that never
            // finishes would otherwise burn GRID_ITEM_COUNT x SETTLE_TIMEOUT.
            var deadline = DateTime.UtcNow.AddSeconds(CONTENT_TIMEOUT);
            foreach (var item in GridItems)
                item.LoadedIndicator.WaitFor(
                    Math.Max((deadline - DateTime.UtcNow).TotalSeconds, 1D), verificationShot: false);

            if (verificationShot)
                Reporter.TakeVerificationShot("loaded_WearablesGridPage");
            Reporter.Log("Wearables grid page finished loading");
        }

        // AltTester serves every command from AltRunner.Update, so any round-trip is a frame;
        // reading the time scale is the one that mutates nothing and reports no step.
        private static void WaitOneFrame() => CommonStuff.AltDriver.GetTimeScale();

        /// <summary>
        /// Returns the grid's first unequipped item, so equip tests are re-runnable regardless
        /// of avatar state. The returned tile is left hovered, ready to equip.
        /// </summary>
        [AllureStep("Find an unequipped grid item")]
        public BackpackGridItem FindUnequippedGridItem()
        {
            for (var i = 0; i < GRID_ITEM_COUNT; i++)
            {
                // Shot-suppressed probe — this is target selection, not a test verification.
                // One shot below records the picked item's hover overlay (Equip visible).
                // Every tile holding content is IsEquipped's precondition, which the callers
                // meet by going through EnsureHairCategory.
                if (GridItems[i].IsEquipped(verificationShot: false))
                    continue;

                Reporter.Log($"Grid item {i} is not equipped — using it");
                Reporter.TakeVerificationShot($"unequipped_GridItem_{i}");
                return GridItems[i];
            }

            throw new AssertionException("Every loaded grid item is equipped — cannot pick a target");
        }

        #endregion
    }

    /// <summary>
    /// Sub-view for the emotes tab within the backpack: ten equip slots on the left and
    /// a paged grid of owned emotes with an item info panel.
    /// </summary>
    public class EmotesTab : BaseView
    {
        #region Elements

        public const int SLOT_COUNT = 10;
        public const int GRID_ITEM_COUNT = 16;
        private const string GRID_PATH = "//Emotes/FullContainer/BackpackGrid";

        public EmoteSlot[] Slots { get; }
        public EmoteGridItem[] GridItems { get; }

        public readonly Readable SelectedItemName = new(By.PATH, "//Emotes/FullContainer/ItemInfoPanel//ItemName");

        #endregion

        #region Setup

        public EmotesTab() : base(new(By.PATH, "//BackpackSection//ContentBackground/Emotes"))
        {
            Slots = new EmoteSlot[SLOT_COUNT];
            for (var i = 0; i < SLOT_COUNT; i++)
            {
                // The first slot container has no " (i)" suffix.
                var slotPath = i == 0
                    ? "//BackpackSection//SlotsContainer/EmoteSlotContainer"
                    : $"//BackpackSection//SlotsContainer/EmoteSlotContainer ({i})";
                Slots[i] = new EmoteSlot(
                    new(By.PATH, slotPath),
                    new(By.PATH, $"{slotPath}//Unequip"),
                    new(By.PATH, $"{slotPath}//EmoteName"),
                    new(By.PATH, $"{slotPath}//EmptyEmoteName"));
            }

            GridItems = new EmoteGridItem[GRID_ITEM_COUNT];
            for (var i = 0; i < GRID_ITEM_COUNT; i++)
            {
                var basePath = $"{GRID_PATH}/BackpackEmoteGridItem(Clone)[{i}]";
                GridItems[i] = new EmoteGridItem(
                    new(By.PATH, basePath),
                    new(By.PATH, $"{basePath}/FullBackpack/HoverBackground/Equip"),
                    new(By.PATH, $"{basePath}/FullBackpack/HoverBackground/Unequip"),
                    new(By.PATH, $"{basePath}/FullBackpack"),
                    new(By.PATH, $"{basePath}/FullBackpack/EquippedSlot"));
            }

            // See WearablesTab.FirstLoadedGridItem — skips stale pooled tiles.
            var loadedPath = $"{GRID_PATH}/BackpackEmoteGridItem(Clone)/FullBackpack";
            FirstLoadedGridItem = new BackpackGridItem(
                new(By.PATH, loadedPath),
                new(By.PATH, $"{loadedPath}/HoverBackground/Equip"),
                new(By.PATH, $"{loadedPath}/HoverBackground/Unequip"),
                new(By.PATH, loadedPath));
        }

        #endregion

        #region Views

        public PageSelector Pager { get; } = new("//Emotes/FullContainer/PageSelector");

        /// <summary>
        /// The first emote grid item that actually has loaded content — safe to click
        /// after a search or page change while pooled stale tiles are still enabled.
        /// </summary>
        public BackpackGridItem FirstLoadedGridItem { get; }

        #endregion

        #region Helper methods

        [AllureStep("Click emote slot")]
        public void ClickSlot(int index)
        {
            Slots[index].Click();
            Reporter.Log($"Clicked emote slot {index}");
        }

        [AllureStep("Click unequip on emote slot")]
        public void ClickUnequip(int index)
        {
            Slots[index].UnequipButton.Click();
            Reporter.Log($"Clicked unequip on emote slot {index}");
        }

        [AllureStep("Click grid item")]
        public void ClickGridItem(int index)
        {
            GridItems[index].Click();
            Reporter.Log($"Clicked grid item {index}");
        }

        /// <summary>
        /// Whether the grid holds a full page of loaded items — the precondition for addressing
        /// tiles by index, since a short page parks blank pooled tiles ahead of the real ones and
        /// a blank answers to its own path while holding nothing.
        /// </summary>
        [AllureStep("Check whether the emote grid page is full")]
        public bool HasFullGridPage() =>
            // Shot-suppressed: a precondition probe, not a verification.
            GridItems[0].LoadedIndicator.IsPresent(verificationShot: false);

        [AllureStep("Wait for a full page of emote grid items")]
        public void WaitForFullGridPage()
        {
            // Paravirt ceiling: a cleared search costs a debounce plus a page request.
            GridItems[0].LoadedIndicator.WaitFor(CONTENT_TIMEOUT);
            Reporter.Log("Emote grid shows a full page of loaded items");
        }

        [AllureStep("Wait for grid item to finish loading")]
        public void WaitForGridItemLoaded(int index)
        {
            // Content first: FullBackpack is the only thing separating a tile that holds an item
            // from a blank pooled one, and IsLoading reads false on both.
            GridItems[index].LoadedIndicator.WaitFor(CONTENT_TIMEOUT, verificationShot: false);

            // Suppress the "appeared" shot — the verified state is IsLoading == false, so the
            // single shot is taken after that wait (a mid-load frame would misrepresent it).
            var gridItem = GridItems[index].WaitFor(UI_TIMEOUT, verificationShot: false);
            gridItem.WaitForComponentProperty<bool>(
                "DCL.Backpack.EmotesSection.BackpackEmoteGridItemView", "IsLoading", false, "Backpack",
                timeout: CONTENT_TIMEOUT);
            Reporter.TakeVerificationShot($"loaded_EmoteGridItem_{index}");
            Reporter.Log($"Grid item {index} finished loading");
        }

        [AllureStep("Unequip all emote slots")]
        public void UnequipAll()
        {
            for (var i = 0; i < SLOT_COUNT; i++)
            {
                UnequipEmoteIfPresent(i);
            }

            Reporter.Log("All emote slots unequipped");
        }

        [AllureStep("Set emote to slot")]
        public void SetEmote(int slotIndex, int gridIndex)
        {
            WaitForGridItemLoaded(gridIndex);
            ClickSlot(slotIndex);
            ClickGridItem(gridIndex);
            GridItems[gridIndex].DoubleClickEquip();
            Reporter.Log($"Set emote grid item {gridIndex} to slot {slotIndex}");
        }

        /// <summary>
        /// Equips the grid's leading loaded item into <paramref name="slotIndex"/>. Use when the
        /// grid is deliberately filtered and index addressing is therefore meaningless —
        /// <c>FirstLoadedGridItem</c> resolves past the blank tiles a short page puts first.
        /// </summary>
        [AllureStep("Set the leading loaded emote to slot")]
        public void SetFirstLoadedEmote(int slotIndex)
        {
            FirstLoadedGridItem.WaitUntilLoaded(CONTENT_TIMEOUT);
            ClickSlot(slotIndex);
            FirstLoadedGridItem.Click();
            FirstLoadedGridItem.DoubleClickEquip();
            Reporter.Log($"Set the leading loaded emote to slot {slotIndex}");
        }

        [AllureStep("Unequip emote slot if present")]
        public void UnequipEmoteIfPresent(int slotIndex)
        {
            ClickSlot(slotIndex);

            if (Slots[slotIndex].UnequipButton.IsPresent())
            {
                ClickUnequip(slotIndex);
                Reporter.Log($"Unequipped emote slot {slotIndex}");
            }
            else
            {
                Reporter.Log($"Emote slot {slotIndex} already empty, skipping");
            }
        }

        /// <summary>
        /// Returns the index of one of the first few grid items that holds an item and is not
        /// equipped in any slot (its EquippedSlot badge is inactive).
        /// </summary>
        [AllureStep("Find an unequipped emote grid item")]
        public int FindUnequippedGridItemIndex(int probeLimit = 12)
        {
            var loadedTiles = 0;

            for (var i = 0; i < probeLimit; i++)
            {
                // Shot-suppressed probes — target selection, not a test verification.

                // A blank tile's badge is absent for the wrong reason: skip it rather than
                // returning a target that can never be equipped.
                if (!GridItems[i].LoadedIndicator.IsPresent(verificationShot: false))
                    continue;

                loadedTiles++;

                if (!GridItems[i].EquippedSlotBadge.IsPresent(verificationShot: false))
                {
                    Reporter.Log($"Emote grid item {i} is not equipped — using it");
                    Reporter.TakeVerificationShot($"unequipped_EmoteGridItem_{i}");
                    return i;
                }
            }

            // Two different failures, two different fixes.
            throw new AssertionException(loadedTiles == 0
                ? $"None of the first {probeLimit} emote grid tiles hold an item — the grid is empty or still filtered by an earlier search"
                : $"All {loadedTiles} loaded tiles among the first {probeLimit} emote grid items are equipped — cannot pick a target");
        }

        #endregion

        #region Sub views

        /// <summary>
        /// A single emote slot in the equipped-emotes bar, with an unequip button and
        /// name labels (EmoteName when occupied, EmptyEmoteName when empty).
        /// </summary>
        public class EmoteSlot(Clickable locator, Clickable unequipLocator, Readable nameLocator, Locatable emptyNameLocator)
            : BaseClickableView(locator)
        {
            #region Elements

            public Clickable UnequipButton  { get; } = unequipLocator;
            public Readable  NameLabel      { get; } = nameLocator;
            public Locatable EmptyNameLabel { get; } = emptyNameLocator;

            #endregion
        }

        /// <summary>
        /// A single emote in the grid. Extends the generic grid item with the EquippedSlot
        /// badge that shows the slot number while the emote is equipped.
        /// </summary>
        public class EmoteGridItem(Clickable root, Clickable equipLocator, Clickable unequipLocator, Locatable loadedLocator, Readable equippedSlotLocator)
            : BackpackGridItem(root, equipLocator, unequipLocator, loadedLocator)
        {
            #region Elements

            public Readable EquippedSlotBadge { get; } = equippedSlotLocator;

            #endregion
        }

        #endregion
    }

    /// <summary>
    /// Sub-view for the Saved Outfits sub-tab: five outfit slots that can each hold a
    /// saved avatar look (empty slots show a hover Save Outfit button; full slots reveal
    /// Equip/Delete buttons on hover).
    /// </summary>
    public class SavedOutfitsTab : BaseView
    {
        #region Elements

        public const int SLOT_COUNT = 5;

        public OutfitSlot[] Slots { get; }

        #endregion

        #region Setup

        public SavedOutfitsTab() : base(new(By.PATH, "//BackpackSection//Avatar/OutfitsView"))
        {
            Slots = new OutfitSlot[SLOT_COUNT];
            for (var i = 0; i < SLOT_COUNT; i++)
                Slots[i] = new OutfitSlot($"//OutfitsView/OutfitSlots/OutfitSlot_{i + 1}");
        }

        #endregion

        #region Helper methods

        /// <summary>
        /// Returns the first slot currently in the Empty state, or null when all five are full.
        /// </summary>
        [AllureStep("Find first empty outfit slot")]
        public OutfitSlot FindFirstEmptySlot()
        {
            for (var i = 0; i < SLOT_COUNT; i++)
            {
                // Shot-suppressed probes — slot selection, not a test verification.
                if (Slots[i].EmptyState.IsPresent(verificationShot: false))
                {
                    Reporter.Log($"Outfit slot {i + 1} is empty");
                    Reporter.TakeVerificationShot($"empty_OutfitSlot_{i + 1}");
                    return Slots[i];
                }
            }

            Reporter.Log("No empty outfit slot found");
            return null;
        }

        /// <summary>
        /// Makes sure the first outfit slot holds a saved outfit, saving the current look
        /// into it when it is empty. Keeps outfit tests re-runnable.
        /// </summary>
        [AllureStep("Ensure the first outfit slot has a saved outfit")]
        public void EnsureFirstSlotSaved()
        {
            if (Slots[0].FullState.IsPresent())
            {
                Reporter.Log("Outfit slot 1 already has a saved outfit");
                return;
            }

            Slots[0].Save();
            Slots[0].FullState.WaitFor(CONTENT_TIMEOUT);
            Reporter.Log("Saved current look into outfit slot 1");
        }

        #endregion

        #region Sub views

        /// <summary>
        /// A single outfit slot. Empty slots expose a hover Save Outfit button; full slots
        /// expose Equip and Delete buttons while hovered.
        /// </summary>
        public class OutfitSlot(string basePath) : BaseClickableView(new(By.PATH, basePath))
        {
            #region Elements

            public readonly Locatable EmptyState  = new(By.PATH, $"{basePath}/LoadedState/Empty");
            public readonly Locatable FullState   = new(By.PATH, $"{basePath}/LoadedState/Full");
            public readonly Clickable SaveButton  = new(By.PATH, $"{basePath}/LoadedState/Hover");
            public readonly Clickable EquipButton  = new(By.PATH, $"{basePath}/LoadedState/Full/ButtonEquip");
            public readonly Clickable DeleteButton = new(By.PATH, $"{basePath}/LoadedState/Full/ButtonDelete");

            #endregion

            #region Helper methods

            /// <summary>
            /// Moves the pointer over the slot so its hover-only buttons become enabled.
            /// Must be followed by a click within the same driver session.
            /// </summary>
            [AllureStep("Hover outfit slot")]
            public AltObject Hover()
            {
                // Shot-suppressed wait: hovering is an action (like Click), not a verification.
                var altObj = WaitFor(UI_TIMEOUT, verificationShot: false);
                altObj.PointerEnter();

                // Wait for a hover-revealed button to actually enable instead of guessing a
                // fixed delay: exactly one of Save/Equip/Delete becomes present depending on
                // whether the slot is empty or full.
                var deadline = DateTime.UtcNow.AddSeconds(OVERLAY_SETTLE);
                while (DateTime.UtcNow < deadline
                       && !SaveButton.IsPresent(verificationShot: false)
                       && !EquipButton.IsPresent(verificationShot: false)
                       && !DeleteButton.IsPresent(verificationShot: false))
                    Thread.Sleep(OVERLAY_POLL_MS);

                return altObj;
            }

            [AllureStep("Save current look into outfit slot")]
            public void Save()
            {
                Hover();
                SaveButton.Click();
                Reporter.Log("Clicked Save Outfit");
            }

            [AllureStep("Equip outfit slot")]
            public void Equip()
            {
                Hover();
                EquipButton.Click();
                Reporter.Log("Clicked Equip on outfit slot");
            }

            /// <summary>
            /// Clicks Delete on the hovered slot. A ConfirmationDialog opens — the caller
            /// must confirm it (see ViewContainer.ConfirmationDialog).
            /// </summary>
            [AllureStep("Delete outfit slot")]
            public void Delete()
            {
                Hover();
                DeleteButton.Click();
                Reporter.Log("Clicked Delete on outfit slot");
            }

            #endregion
        }

        #endregion
    }

    #endregion
}
