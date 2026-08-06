namespace ExplorerAutomation.Tests.Views;

/// <summary>
/// View for the event details popup that appears when clicking an event card in the
/// explore panel's Events section. The panel lives at the scene root (not inside
/// ExplorePanelUI) and is disabled — not destroyed — when closed.
/// </summary>
public class EventDetailView() : BaseView(new(By.NAME, "EventDetailPanel(Clone)"))
{
    #region Elements

    public readonly Clickable CloseButton      = new(By.PATH, "//EventDetailPanel(Clone)//Button_Close");
    public readonly Readable  EventName        = new(By.PATH, "//EventDetailPanel(Clone)//EventHeader//EventName");
    // Rich text, e.g. "Hosted by <b>RegenesisLabs</b>".
    public readonly Readable  Host             = new(By.PATH, "//EventDetailPanel(Clone)//EventHeader//Host");
    // "Started 3 hour ago" for live events, a schedule date otherwise.
    public readonly Readable  TimeText         = new(By.PATH, "//EventDetailPanel(Clone)//EventHeader//TimeText");
    public readonly Readable  DescriptionTitle = new(By.PATH, "//EventDetailPanel(Clone)//DescriptionTitle");
    public readonly Readable  Description      = new(By.PATH, "//EventDetailPanel(Clone)//Description");
    // Bottom "place" row, e.g. "Spawn & Chill (chillzone.dcl.eth)".
    public readonly Readable  PlaceName        = new(By.PATH, "//EventDetailPanel(Clone)//Place/PlaceName");
    // Header action buttons. JumpIn is only enabled for live events; Interested ("REMIND ME")
    // only for future ones.
    public readonly Clickable JumpInButton        = new(By.PATH, "//EventDetailPanel(Clone)//Buttons/JumpIn");
    public readonly Clickable InterestedButton    = new(By.PATH, "//EventDetailPanel(Clone)//Buttons/Interested");
    public readonly Clickable AddToCalendarButton = new(By.PATH, "//EventDetailPanel(Clone)//Buttons/AddToCalendar");
    public readonly Clickable ShareButton         = new(By.PATH, "//EventDetailPanel(Clone)//Buttons/Share");

    #endregion
}
