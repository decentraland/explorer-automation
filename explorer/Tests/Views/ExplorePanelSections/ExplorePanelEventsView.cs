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

    // Big cards (live/highlighted events) in the "Today" column of the calendar. The column
    // only contains EventCard_Big(Clone) instances when live events exist right now.
    public EventCard[] TodayBigCards { get; }
    // Small cards in the "Tomorrow" column of the calendar — scheduled (non-live) events.
    public EventCard[] TomorrowSmallCards { get; }

    #endregion

    #region Setup

    public ExplorePanelEventsView() : base(new(By.NAME, "EventsSection"))
    {
        DaySelectorButtons = new Clickable[DAY_COUNT];
        for (var i = 0; i < DAY_COUNT; i++)
            DaySelectorButtons[i] = new(By.PATH, $"//EventsSection//DaysSelectorContainer/Day{i + 1}SelectorButton");

        TodayBigCards = new EventCard[BIG_CARDS];
        for (var i = 0; i < BIG_CARDS; i++)
            TodayBigCards[i] = new EventCard(
                $"//EventsSection//Day1EventsListView//EventsContainer/EventCard_Big(Clone)[{i}]",
                "Footer/Texts/EventName",
                "Footer/Texts/Host");

        TomorrowSmallCards = new EventCard[SMALL_CARDS];
        for (var i = 0; i < SMALL_CARDS; i++)
            TomorrowSmallCards[i] = new EventCard(
                $"//EventsSection//Day2EventsListView//EventsContainer/EventCard_Small(Clone)[{i}]",
                "NameAndHostContainer/Name",
                "NameAndHostContainer/Host");
    }

    #endregion

    #region Views

    public EventDetailView EventDetail { get; } = new();

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
