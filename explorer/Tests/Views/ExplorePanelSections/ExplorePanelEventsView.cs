namespace ExplorerAutomation.Tests.Views.ExplorePanelSections;

/// <summary>
/// Section view for the Events tab within the explore panel, displaying upcoming Decentraland events.
/// Two display modes share the section: EventsCalendar (default — five side-by-side day columns
/// with a day-selector strip on top) and EventsByDay (single-day results list, shown after
/// clicking a non-today day selector). Only one of the two is enabled at a time.
/// </summary>
public class ExplorePanelEventsView : BaseSection
{
    #region Elements

    private const int DAY_COUNT     = 5;
    private const int BIG_CARDS     = 4;
    private const int SMALL_CARDS   = 8;
    // Only ever spent re-reading a card a presence probe just resolved.
    private const double CARD_TIMEOUT = 5D;

    public readonly Clickable GoToTodayButton   = new(By.PATH, "//EventsSection//Header/GoToTodayButton");
    // Opens the event-creation flow in an external browser — do not click in automation,
    // it steals OS focus from the client and breaks AltTester input.
    public readonly Clickable CreateEventButton = new(By.PATH, "//EventsSection//Header/CreateEventButton");

    public readonly Locatable EventsCalendar    = new(By.PATH, "//EventsSection//EventsCalendar");
    public readonly Locatable EventsByDay       = new(By.PATH, "//EventsSection//EventsByDay");
    // e.g. "Fri, Aug 07 (9)" — only enabled while the by-day mode is active.
    public readonly Readable  ByDayResultsCounter = new(By.PATH, "//EventsSection//EventsByDay//ResultsCounter");
    public readonly Clickable ByDayBackButton     = new(By.PATH, "//EventsSection//EventsByDay//BackButton");

    // Day1SelectorButton..Day5SelectorButton (day 1 = today). Clicking day 1 keeps the
    // calendar; clicking any other day switches the section to the EventsByDay mode.
    public Clickable[] DaySelectorButtons { get; }

    // Cards per day column, day 1..5 left to right, big prefabs then small — either prefab can
    // fill any column. Indices are hierarchy slots, not positions: click via FindTopLeftVisibleCard.
    public EventCard[][] DayCards { get; }

    #endregion

    #region Setup

    public ExplorePanelEventsView() : base(new(By.NAME, "EventsSection"))
    {
        DaySelectorButtons = new Clickable[DAY_COUNT];
        for (var i = 0; i < DAY_COUNT; i++)
            DaySelectorButtons[i] = new(By.PATH, $"//EventsSection//DaysSelectorContainer/Day{i + 1}SelectorButton");

        DayCards = new EventCard[DAY_COUNT][];
        for (var day = 0; day < DAY_COUNT; day++)
            DayCards[day] = BuildDayCards($"//EventsSection//Day{day + 1}EventsListView//EventsContainer");
    }

    private static EventCard[] BuildDayCards(string containerPath)
    {
        var cards = new EventCard[BIG_CARDS + SMALL_CARDS];
        for (var i = 0; i < BIG_CARDS; i++)
            cards[i] = new EventCard(
                $"{containerPath}/EventCard_Big(Clone)[{i}]",
                "Footer/Texts/EventName",
                "Footer/Texts/Host");

        for (var i = 0; i < SMALL_CARDS; i++)
            cards[BIG_CARDS + i] = new EventCard(
                $"{containerPath}/EventCard_Small(Clone)[{i}]",
                "NameAndHostContainer/Name",
                "NameAndHostContainer/Host");

        return cards;
    }

    #endregion

    #region Views

    public EventDetailView EventDetail { get; } = new();

    #endregion

    #region Helper methods

    /// <summary>
    /// Returns the left-top-most event card inside the viewport — the only safe click target,
    /// since a pooled column's hierarchy index can name a card parked off-screen.
    /// </summary>
    [AllureStep("Find the left-top-most visible event card")]
    public EventCard FindTopLeftVisibleCard()
    {
        // Left to right: the first column with a visible card answers, the rest stay unprobed.
        for (var day = 0; day < DAY_COUNT; day++)
        {
            EventCard topMost = null;
            var topMostY = int.MaxValue;
            var topMostX = 0;

            foreach (var card in DayCards[day])
            {
                // Shot-suppressed probes — target selection, not a test verification.
                if (!card.IsPresent(verificationShot: false))
                    continue;

                var obj = card.WaitFor(CARD_TIMEOUT, verificationShot: false);
                if (!IsOnScreen(obj) || obj.mobileY >= topMostY)
                    continue;

                topMost  = card;
                topMostY = obj.mobileY;
                topMostX = obj.x;
            }

            if (topMost is null)
                continue;

            Reporter.Log($"Picked the event card at x={topMostX}, mobileY={topMostY} in day column {day + 1}");
            return topMost;
        }

        throw new AssertionException(
            "No event card is both present and on screen in any calendar day column — the calendar is empty or never finished loading");
    }

    // Centre inside both edges — y is bottom-origin, mobileY top-origin, so no screen size needed.
    private static bool IsOnScreen(AltObject card) => card.y > 0 && card.mobileY > 0;

    #endregion

    #region Sub views

    /// <summary>
    /// Clickable view for a single event card in a day column. Big and small card prefabs
    /// place the name/host labels at different relative paths, so the paths are injected.
    /// </summary>
    public class EventCard : BaseClickableView
    {
        #region Elements

        public readonly Readable EventName;
        public readonly Readable Host;

        #endregion

        #region Setup

        public EventCard(string basePath, string namePath, string hostPath) : base(new(By.PATH, basePath))
        {
            EventName = new(By.PATH, $"{basePath}/{namePath}");
            Host      = new(By.PATH, $"{basePath}/{hostPath}");
        }

        #endregion
    }

    #endregion
}
