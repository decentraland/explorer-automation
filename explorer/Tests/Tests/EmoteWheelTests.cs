namespace ExplorerAutomation.Tests.Tests;

// Emote wheel notes for this build (dev_b97439fc):
//   - There is no in-HUD signal that an emote ANIMATION is actually playing (the avatar
//     animation is not uGUI). The observable outcome of triggering an emote is that the
//     wheel closes itself; the TearDown screenshot captures the avatar pose as evidence.
//   - B+number ("Hold [b+num] to run an emote while the wheel is closed") has no observable
//     UI state at all, so it is not asserted here — the wheel-click path covers triggering.
[AllureSuite("Emote Wheel Tests")]
[Category("InWorld")]
[Order(19)]
public class EmoteWheelTests : BaseTest
{
    [Test]
    public void TestOpenEmoteWheelFromSidebar()
    {
        Views.MainMenu.EmoteWheelButton.Click();
        Views.EmotesWheel.WaitFor();
        Assert.That(Views.EmotesWheel.TitleLabel.GetText(), Is.EqualTo("Emotes"),
            "Emote wheel should show its Emotes title");
        Reporter.Log("Emote wheel opened from the sidebar emotes button");

        PressEscape();
        Views.EmotesWheel.WaitForGone();
        Reporter.Log("Emote wheel closed with Escape");
    }

    [Test]
    public void TestOpenEmoteWheelWithShortcutShowsAllSlots()
    {
        PressKey(AltKeyCode.B);
        Views.EmotesWheel.WaitFor();
        Reporter.Log("Emote wheel opened with the B shortcut");

        var missingSlots = new List<int>();
        for (var i = 0; i < EmotesWheelView.SLOT_COUNT; i++)
        {
            if (!Views.EmotesWheel.Slots[i].IsPresent())
                missingSlots.Add(i);
        }

        Assert.That(missingSlots, Is.Empty,
            $"All {EmotesWheelView.SLOT_COUNT} wheel slots should be present, missing: {string.Join(", ", missingSlots)}");
        Reporter.Log($"All {EmotesWheelView.SLOT_COUNT} emote slots present on the wheel");

        // B toggles: a second press closes the wheel again.
        PressKey(AltKeyCode.B);
        Views.EmotesWheel.WaitForGone();
        Reporter.Log("Emote wheel closed with a second B press");
    }

    [Test]
    public void TestTriggerEmoteFromWheelClosesWheel()
    {
        PressKey(AltKeyCode.B);
        Views.EmotesWheel.WaitFor();
        Wait(0.5); // show-animation guard before clicking a slot

        var playedSlot = Views.EmotesWheel.PlayFirstLoadedEmote();
        Views.EmotesWheel.WaitForGone();
        Reporter.Log($"Emote triggered from wheel slot {playedSlot} — wheel closed itself " +
                     "(avatar animation has no uGUI signal; see the TearDown screenshot)");
    }
}
