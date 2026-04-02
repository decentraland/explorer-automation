namespace ExplorerAutomation.Tests.Views.ExplorePanelSections;

public class ExplorePanelBackpackView() : BaseSection(new(By.ID, "1666be29-f174-43c8-98d0-c9b02bc0d011"))
{
    public readonly Clickable WearablesTabButton = new(By.PATH, "//TabSelector/Avatar");
    public readonly Clickable EmotesTabButton    = new(By.PATH, "//TabSelector/Emotes");
    public readonly Writable  SearchBar          = new(By.PATH, "//BackpackSection//SearchBar");

    public EmotesTab Emotes { get; } = new();

    public class EmotesTab : BaseView
    {
        private const int SLOT_COUNT      = 10;
        private const int GRID_ITEM_COUNT = 16;

        public EmoteSlot[] Slots { get; }
        public EmoteGridItem[] GridItems { get; }

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

        [AllureStep("Click equip on grid item")]
        public void ClickGridItemEquip(int index)
        {
            GridItems[index].EquipButton.Click();
            Reporter.Log($"Clicked equip on grid item {index}");
        }

        [AllureStep("Click unequip on grid item")]
        public void ClickGridItemUnequip(int index)
        {
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

        public class EmoteSlot(Clickable locator, Clickable unequipLocator) : BaseClickableView(locator)
        {
            public Clickable UnequipButton { get; } = unequipLocator;
        }

        public class EmoteGridItem(Clickable root, Clickable equipLocator, Clickable unequipLocator) : BaseClickableView(root)
        {
            public Clickable EquipButton   { get; } = equipLocator;
            public Clickable UnequipButton { get; } = unequipLocator;
        }
    }
}