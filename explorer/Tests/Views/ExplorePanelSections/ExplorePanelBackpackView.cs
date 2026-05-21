namespace ExplorerAutomation.Tests.Views.ExplorePanelSections;

/// <summary>
/// Section view for the Backpack tab within the explore panel, where users manage
/// their equipped wearables and emotes.
/// </summary>
public class ExplorePanelBackpackView() : BaseSection(new(By.NAME, "BackpackSection"))
{
    #region Elements

    public readonly Clickable WearablesTabButton = new(By.PATH, "//TabSelector/Avatar");
    public readonly Clickable EmotesTabButton = new(By.PATH, "//TabSelector/Emotes");
    // NOTE: verify exact GameObject name via AltTester inspector if path fails
    public readonly Clickable SavedOutfitsTabButton = new(By.PATH, "//TabSelector/ToggleOutfits");
    public readonly Writable SearchBar = new(By.PATH, "//BackpackSection//SearchBar");

    #endregion

    #region Views

    public WearablesTab Wearables { get; } = new();
    public EmotesTab Emotes { get; } = new();
    public SavedOutfitsTab SavedOutfits { get; } = new();

    /// <summary>
    /// Sub-view for the wearables tab within the backpack, containing a scrollable grid
    /// of owned wearables that can be equipped via hover.
    /// </summary>
    public class WearablesTab : BaseView
    {
        #region Elements

        // NOTE: verify grid item GameObject name via AltTester inspector if paths fail
        private const int GRID_ITEM_COUNT = 16;

        public WearableGridItem[] GridItems { get; }

        public readonly Clickable AvatarSlotHair = new(By.PATH, "//SlotsContainer/AvatarSlotHair");

        #endregion

        #region Setup

        public WearablesTab() : base(new(By.ID, "TODO"))
        {
            GridItems = new WearableGridItem[GRID_ITEM_COUNT];
            for (var i = 0; i < GRID_ITEM_COUNT; i++)
            {
                var basePath = $"//BackpackGrid/BackpackWearableGridItem(Clone)[{GRID_ITEM_COUNT - i - 1}]";
                GridItems[i] = new WearableGridItem(
                    new(By.PATH, basePath),
                    new(By.PATH, $"{basePath}/FullBackpack/HoverBackground/Equip"));
            }
        }

        #endregion

        #region Views

        /// <summary>
        /// Clickable view representing a single wearable in the grid,
        /// with an equip button revealed on hover.
        /// </summary>
        public class WearableGridItem(Clickable root, Clickable equipLocator) : BaseClickableView(root)
        {
            #region Elements

            public Clickable EquipButton { get; } = equipLocator;

            #endregion
        }

        #endregion

        #region Helper methods

        [AllureStep("Click wearable grid item")]
        public void ClickGridItem(int index)
        {
            GridItems[index].Click();
            Reporter.Log($"Clicked wearable grid item {index}");
        }

        [AllureStep("Equip wearable grid item via hover and click")]
        public void ClickGridItemEquip(int index)
        {
            // Hover to reveal the HoverBackground overlay, then click the Equip button.
            // Unlike emotes (which support a double-click shortcut), wearables require
            // the real hover path to reach the equip action.
            var altObj = GridItems[index].WaitFor();
            altObj.PointerEnter();
            Thread.Sleep(300);
            GridItems[index].EquipButton.Click();
            Reporter.Log($"Hovered and clicked equip on wearable grid item {index}");
        }

        [AllureStep("Click search result wearable and equip")]
        public void ClickSearchResultAndEquip(int index)
        {
            // When search filters to a single visible item, AltTester sees no siblings so
            // the object has no index suffix. With multiple visible results, [n] applies.
            var basePath = index == 0
                ? "//BackpackGrid/BackpackWearableGridItem(Clone)"
                : $"//BackpackGrid/BackpackWearableGridItem(Clone)[{index}]";
            var result = new WearableGridItem(
                new(By.PATH, basePath),
                new(By.PATH, $"{basePath}/FullBackpack/HoverBackground/Equip"));
            result.WaitFor();
            result.Click();
            Thread.Sleep(300);
            result.EquipButton.Click();
            Reporter.Log($"Clicked search result {index} and equipped");
        }

        [AllureStep("Click first item in BackpackGrid after slot filter and equip")]
        public void ClickFirstBackpackItem()
        {
            var item = new Clickable(By.PATH, "//BackpackGrid/BackpackItem(Clone)[15]");
            var equip = new Clickable(By.PATH, "//BackpackGrid/BackpackItem(Clone)[15]/FullBackpack/HoverBackground/Equip");
            var unequip = new Clickable(By.PATH, "//BackpackGrid/BackpackItem(Clone)[15]/FullBackpack/HoverBackground/Unequip");
            item.Click();
            Reporter.Log("Clicked first BackpackItem in grid");
            equip.WaitFor();
            equip.Click();
            Reporter.Log("Equip button clicked");
            unequip.WaitFor();
            unequip.Click();
            Reporter.Log("Unequip button clicked");

        }

        #endregion
    }

    /// <summary>
    /// Sub-view for the emotes tab within the backpack, containing emote slots (equipped)
    /// and a scrollable grid of available emotes.
    /// </summary>
    public class EmotesTab : BaseView
    {
        #region Elements

        private const int SLOT_COUNT = 10;
        private const int GRID_ITEM_COUNT = 16;

        public EmoteSlot[] Slots { get; }
        public EmoteGridItem[] GridItems { get; }

        #endregion

        #region Setup

        public EmotesTab() : base(new(By.ID, "TODO"))
        {
            Slots = new EmoteSlot[SLOT_COUNT];
            Slots[0] = new EmoteSlot(
                new(By.PATH, "//BackpackSection//EmoteSlotContainer"),
                new(By.PATH, "//BackpackSection//EmoteSlotContainer//Unequip"));
            for (var i = 1; i < SLOT_COUNT; i++)
                Slots[i] = new EmoteSlot(
                    new(By.PATH, $"//BackpackSection//EmoteSlotContainer ({i})"),
                    new(By.PATH, $"//BackpackSection//EmoteSlotContainer ({i})//Unequip"));

            GridItems = new EmoteGridItem[GRID_ITEM_COUNT];
            for (var i = 0; i < GRID_ITEM_COUNT; i++)
            {
                var basePath = $"//BackpackGrid/BackpackEmoteGridItem(Clone)[{GRID_ITEM_COUNT - i - 1}]";
                GridItems[i] = new EmoteGridItem(
                    new(By.PATH, basePath),
                    new(By.PATH, $"{basePath}/FullBackpack/HoverBackground/Equip"),
                    new(By.PATH, $"{basePath}/FullBackpack/HoverBackground/Unequip"));
            }
        }

        #endregion

        #region Views

        /// <summary>
        /// Clickable view representing a single emote slot in the equipped-emotes bar,
        /// with an optional unequip button.
        /// </summary>
        public class EmoteSlot(Clickable locator, Clickable unequipLocator) : BaseClickableView(locator)
        {
            #region Elements

            public Clickable UnequipButton { get; } = unequipLocator;

            #endregion
        }

        /// <summary>
        /// Clickable view representing a single emote in the available-emotes grid,
        /// with equip and unequip buttons shown on hover.
        /// </summary>
        public class EmoteGridItem(Clickable root, Clickable equipLocator, Clickable unequipLocator) : BaseClickableView(root)
        {
            #region Elements

            public Clickable EquipButton { get; } = equipLocator;
            public Clickable UnequipButton { get; } = unequipLocator;

            #endregion
        }

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

        [AllureStep("Equip grid item via double-click")]
        public void ClickGridItemEquip(int index)
        {
            // BackpackItemView.OnPointerClick treats clickCount==2 as Equip (and 1 as Select).
            // The Equip button itself lives inside HoverBackground which only animates in on
            // a real mouse hover; AltTester's PointerEnter doesn't reliably trigger that
            // overlay. Double-clicking the grid item bypasses the hover overlay entirely
            // and routes directly to OnEquip via the IPointerClickHandler logic.
            var altObj = GridItems[index].WaitFor();
            altObj.Click();
            Thread.Sleep(80);
            altObj.Click();
            Reporter.Log($"Double-clicked grid item {index} to equip");
        }

        [AllureStep("Click unequip on grid item")]
        public void ClickGridItemUnequip(int index)
        {
            // No equivalent double-click shortcut for unequip — has to come through the
            // HoverBackground/Unequip button. PointerEnter to reveal it, then click.
            var altObj = GridItems[index].WaitFor();
            altObj.PointerEnter();
            Thread.Sleep(300);
            GridItems[index].UnequipButton.Click();
            Reporter.Log($"Clicked unequip on grid item {index}");
        }

        [AllureStep("Wait for grid item to finish loading")]
        private void WaitForGridItemLoaded(int index)
        {
            var gridItem = GridItems[index].WaitFor();
            gridItem.WaitForComponentProperty<bool>(
                "DCL.Backpack.EmotesSection.BackpackEmoteGridItemView", "IsLoading", false, "Backpack", timeout: 10);
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
            ClickGridItemEquip(gridIndex);
            Reporter.Log($"Set emote grid item {gridIndex} to slot {slotIndex}");
        }

        [AllureStep("Get ID / URN of a grid item emote")]
        private void GetGridItemEmoteID(int index)
        {
            var gridItem = GridItems[index].WaitFor();
            var urn = gridItem.GetComponentProperty<string>(
                "DCL.Backpack.EmotesSection.BackpackEmoteGridItemView", "ItemId", "Backpack");
            Reporter.Log($"Slot item {index} has URN: {urn}");
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

        #endregion
    }

    /// <summary>
    /// Sub-view for the saved outfits tab within the backpack, containing a grid of
    /// previously saved outfit combinations that can be selected and equipped.
    /// </summary>
    public class SavedOutfitsTab : BaseView
    {
        #region Elements

        // NOTE: verify grid item GameObject name and container path via AltTester inspector
        private const int GRID_ITEM_COUNT = 6;

        public OutfitGridItem[] GridItems { get; }

        #endregion

        #region Setup

        public SavedOutfitsTab() : base(new(By.ID, "TODO"))
        {
            GridItems = new OutfitGridItem[GRID_ITEM_COUNT];
            for (var i = 0; i < GRID_ITEM_COUNT; i++)
            {
                var basePath = $"//OutfitsSection/OutfitGridItem(Clone)[{i}]";
                GridItems[i] = new OutfitGridItem(
                    new(By.PATH, basePath),
                    new(By.PATH, $"{basePath}//EquipButton"));
            }
        }

        #endregion

        #region Views

        /// <summary>
        /// Clickable view representing a single saved outfit card in the grid,
        /// with a dedicated equip button.
        /// </summary>
        public class OutfitGridItem(Clickable root, Clickable equipLocator) : BaseClickableView(root)
        {
            #region Elements

            public Clickable EquipButton { get; } = equipLocator;

            #endregion
        }

        #endregion

        #region Helper methods

        [AllureStep("Click outfit grid item")]
        public void ClickGridItem(int index)
        {
            GridItems[index].Click();
            Reporter.Log($"Clicked outfit grid item {index}");
        }

        [AllureStep("Click equip on outfit grid item")]
        public void ClickGridItemEquip(int index)
        {
            GridItems[index].EquipButton.Click();
            Reporter.Log($"Clicked equip on outfit grid item {index}");
        }

        #endregion
    }

    #endregion
}
