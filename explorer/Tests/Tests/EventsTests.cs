namespace ExplorerAutomation.Tests.Tests;

// Depth coverage for the explore panel's Events section (the open-from-sidebar smoke test
// lives in ExplorePanelTests). The in-world Order band 10-19 is full, so this fixture
// shares Order 11 with ExplorePanelTests (duplicate Orders have precedent at 16 and 19).
[AllureSuite("Events Tests")]
[Category("InWorld")]
[Order(11)]
public class EventsTests : BaseTest
{
    [Test]
    public void TestSwitchEventsDay()
    {
        OpenEvents();
        Views.ExplorePanel.Events.EventsCalendar.WaitFor();

        // Clicking a non-today day selector swaps the calendar for the single-day list.
        Views.ExplorePanel.Events.DaySelectorButtons[2].Click();
        Views.ExplorePanel.Events.EventsByDay.WaitFor();

        // The counter first shows just the day ("Fri, Aug 07") and appends the event count
        // ("(9)") once the day's events finish loading — poll until the count arrives.
        var counter = Views.ExplorePanel.Events.ByDayResultsCounter.GetText();
        for (var attempt = 0; attempt < 10 && !counter.Contains('('); attempt++)
        {
            Wait(1);
            counter = Views.ExplorePanel.Events.ByDayResultsCounter.GetText();
        }

        Assert.That(counter, Does.Match(@"\(\d+\)"),
            "Single-day view should show a '<day> (N)' results counter");
        Reporter.Log($"Switched to single-day view: {counter}");

        Views.ExplorePanel.Events.GoToTodayButton.Click();
        Views.ExplorePanel.Events.EventsCalendar.WaitFor();
        Views.ExplorePanel.Events.EventsByDay.WaitForGone();
        Reporter.Log("Returned to the calendar view via Today button");

        Views.ExplorePanel.Close();
    }

    [Test]
    public void TestOpenEventDetail()
    {
        OpenEvents();
        Views.ExplorePanel.Events.EventsCalendar.WaitFor();
        // Day columns populate asynchronously — wait for a candidate card instead of a fixed
        // pause; the re-binding that follows is the retry loop's job below.
        WaitUntil(() => Views.ExplorePanel.Events.TodayBigCards[0].IsPresent(verificationShot: false)
                        || Views.ExplorePanel.Events.TomorrowSmallCards[0].IsPresent(verificationShot: false), 2);

        // The Today column only holds big cards while events are live, so fall back to
        // tomorrow's first (always-scheduled) small card when nothing is live right now.
        // Card resolution + name capture happen inside the retry: the calendar re-binds
        // cards while events stream in, silently dropping clicks and moving anchors.
        var eventName = string.Empty;
        ClickUntil(() =>
        {
            var card = Views.ExplorePanel.Events.TodayBigCards[0];
            if (!card.IsPresent())
            {
                Reporter.Log("No live event card in the Today column — using tomorrow's first card");
                card = Views.ExplorePanel.Events.TomorrowSmallCards[0];
            }

            eventName = card.EventName.GetText();
            card.Click();
        }, () => Views.ExplorePanel.Events.EventDetail.IsPresent());

        Views.ExplorePanel.Events.EventDetail.WaitFor();
        Assert.That(Views.ExplorePanel.Events.EventDetail.EventName.GetText(), Is.EqualTo(eventName),
            "Event detail should show the clicked event's name");
        Assert.That(Views.ExplorePanel.Events.EventDetail.DescriptionTitle.IsPresent(), Is.True,
            "Event detail should contain a DESCRIPTION block");
        Reporter.Log($"Event detail opened for '{eventName}'");

        Views.ExplorePanel.Events.EventDetail.CloseButton.Click();
        Views.ExplorePanel.Events.EventDetail.WaitForGone();

        Views.ExplorePanel.Close();
    }

    [Test]
    public void TestCreateEventButtonPresent()
    {
        OpenEvents();

        // Presence-only on purpose: clicking CREATE EVENT opens the event-creation flow in
        // an external browser, which steals OS focus from the client and breaks AltTester
        // input for the rest of the run.
        Assert.That(Views.ExplorePanel.Events.CreateEventButton.IsPresent(), Is.True,
            "Events header should offer the CREATE EVENT button");
        Assert.That(Views.ExplorePanel.Events.GoToTodayButton.IsPresent(), Is.True,
            "Events header should offer the Today button");
        Reporter.Log("Create Event and Today buttons present in the Events header");

        Views.ExplorePanel.Close();
    }

    /// <summary>
    /// Opens the Events section via the keyboard shortcut. Deliberately NOT the sidebar
    /// button — see PlacesTests.OpenPlaces for the stale open-section-state rationale.
    /// The sidebar-click entry path is covered by ExplorePanelTests/ShortcutsTests.
    /// </summary>
    private void OpenEvents()
    {
        ClickUntil(() => PressKey(AltKeyCode.X, delay: 0),
                   () => Views.ExplorePanel.Events.IsPresent());
        Views.ExplorePanel.Events.WaitFor();
    }
}
