namespace ExplorerAutomation.Tests.Views;

/// <summary>
/// View for the radial emote wheel overlay. Opens by pressing B or clicking the sidebar
/// emotes button; closes on Escape, a second B press, or after playing an emote. The
/// EmotesWheelHUD prefab is instantiated lazily on first open and toggled by
/// enabling/disabling afterwards, so the root is only findable while the wheel is open.
/// </summary>
public class EmotesWheelView : BaseView
{
    protected override string ViewName => "EmotesWheelView";

    #region Elements

    public const int SLOT_COUNT = 10;

    public readonly Readable  TitleLabel            = new(By.PATH, "//EmotesWheelHUD(Clone)//Title");
    // Shows the hovered slot's emote name in the wheel center.
    public readonly Readable  CurrentEmoteNameLabel = new(By.PATH, "//EmotesWheelHUD(Clone)//EmoteName");
    public readonly Clickable CloseButton           = new(By.PATH, "//EmotesWheelHUD(Clone)//CloseButton");
    public readonly Clickable EditButton            = new(By.PATH, "//EmotesWheelHUD(Clone)//EditButton");

    public EmoteSlot[] Slots { get; }

    #endregion

    #region Setup

    public EmotesWheelView() : base(new(By.NAME, "EmotesWheelHUD(Clone)"))
    {
        Slots = new EmoteSlot[SLOT_COUNT];
        for (var i = 0; i < SLOT_COUNT; i++)
            Slots[i] = new EmoteSlot(
                new(By.PATH, $"//EmotesWheelHUD(Clone)//Slot{i}"),
                new(By.PATH, $"//EmotesWheelHUD(Clone)//Slot{i}//RarityContainer"),
                new(By.PATH, $"//EmotesWheelHUD(Clone)//Slot{i}//Thumbnail"));
    }

    #endregion

    #region Helper methods

    /// <summary>
    /// Clicks the play button of the first slot that has an emote loaded (enabled thumbnail)
    /// and returns its index. Slots can be empty when backpack tests unequipped them, so we
    /// pick dynamically instead of hardcoding a slot number.
    /// </summary>
    [AllureStep("Play the first loaded emote on the wheel")]
    public int PlayFirstLoadedEmote()
    {
        for (var i = 0; i < SLOT_COUNT; i++)
        {
            // Shot-suppressed probes — slot selection, not a test verification. One shot at
            // the pick records the wheel state with the chosen slot's thumbnail loaded.
            if (!Slots[i].Thumbnail.IsPresent(verificationShot: false))
                continue;

            Reporter.Log($"Playing emote from wheel slot {i}");
            Reporter.TakeVerificationShot($"loaded_EmoteWheelSlot_{i}");
            Slots[i].PlayButton.Click();
            return i;
        }

        throw new AssertionException("No emote wheel slot has an emote loaded — cannot trigger an emote");
    }

    #endregion

    #region Sub views

    /// <summary>
    /// A single radial slot on the emote wheel. The play button (RarityContainer) triggers
    /// the emote and closes the wheel; the thumbnail is only enabled when an emote is
    /// equipped and loaded in that slot.
    /// </summary>
    public class EmoteSlot(Locatable root, Clickable playButton, Locatable thumbnail) : BaseView(root)
    {
        #region Elements

        public Clickable PlayButton { get; } = playButton;
        public Locatable Thumbnail  { get; } = thumbnail;

        #endregion
    }

    #endregion
}
