namespace ExplorerAutomation.Tests.Views.ExplorePanelSections;

public class BackpackSection : BaseSection
{
    private readonly (By, string) _wearablesTabLocator = (By.PATH, "//TabSelector/Avatar");
    private readonly (By, string) _emotesTabLocator    = (By.PATH, "//TabSelector/Emotes");

    public EmotesTab Emotes { get; }

    public BackpackSection(AltDriver altDriver)
        : base(altDriver, (By.ID, "1666be29-f174-43c8-98d0-c9b02bc0d011"))
    {
        Emotes = new EmotesTab(altDriver);
    }

    [AllureStep("Click Wearables tab")]
    public void ClickWearablesTab()
    {
        ClickObject(_wearablesTabLocator);
        Reporter.Log("Clicked Wearables tab");
    }

    [AllureStep("Click Emotes tab")]
    public void ClickEmotesTab()
    {
        ClickObject(_emotesTabLocator);
        Reporter.Log("Clicked Emotes tab");
    }

    public class EmotesTab : BaseView
    {
        private const int SLOT_COUNT = 10;
        private const int GRID_ITEM_COUNT = 16;

        public EmoteSlot[] Slots { get; }

        private readonly (By, string)[] _gridItemLocators;

        public EmotesTab(AltDriver altDriver) : base(altDriver)
        {
            Slots = new EmoteSlot[SLOT_COUNT];
            Slots[0] = new EmoteSlot(
                (By.PATH, "//BackpackSection//EmoteSlotContainer"),
                (By.PATH, "//BackpackSection//EmoteSlotContainer//Unequip"));
            for (var i = 1; i < SLOT_COUNT; i++)
                Slots[i] = new EmoteSlot(
                    (By.PATH, $"//BackpackSection//EmoteSlotContainer ({i})"),
                    (By.PATH, $"//BackpackSection//EmoteSlotContainer ({i})//Unequip"));

            _gridItemLocators = new (By, string)[GRID_ITEM_COUNT];
            for (var i = 0; i < GRID_ITEM_COUNT; i++)
                _gridItemLocators[i] = (By.PATH, $"//BackpackGrid/BackpackEmoteGridItem(Clone)[{i}]");
        }

        [AllureStep("Click emote slot")]
        public void ClickSlot(int index)
        {
            ClickObject(Slots[index].Locator);
            Reporter.Log($"Clicked emote slot {index}");
        }

        [AllureStep("Click unequip on emote slot")]
        public void ClickUnequip(int index)
        {
            ClickObject(Slots[index].UnequipLocator);
            Reporter.Log($"Clicked unequip on emote slot {index}");
        }

        [AllureStep("Click grid item")]
        public void ClickGridItem(int index)
        {
            ClickObject(_gridItemLocators[index]);
            Reporter.Log($"Clicked grid item {index}");
        }

        [AllureStep("Set emote to slot")]
        public void SetEmote(int slotIndex, int gridIndex)
        {
            ClickSlot(slotIndex);
            ClickGridItem(gridIndex);
            Reporter.Log($"Set emote grid item {gridIndex} to slot {slotIndex}");
        }

        public class EmoteSlot
        {
            public (By, string) Locator { get; }
            public (By, string) UnequipLocator { get; }

            public EmoteSlot((By, string) locator, (By, string) unequipLocator)
            {
                Locator = locator;
                UnequipLocator = unequipLocator;
            }
        }
    }
}
