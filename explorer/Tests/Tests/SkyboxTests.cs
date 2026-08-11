namespace ExplorerAutomation.Tests.Tests;

[AllureSuite("Skybox Tests")]
[Category("InWorld")]
[Order(19)]
public class SkyboxTests : BaseTest
{
    [Test]
    public void TestSkyboxWidgetPresence()
    {
        OpenSkyboxMenu();

        Assert.That(Views.MainMenu.Skybox.TitleLabel.GetText(), Is.EqualTo("Night/day"),
            "Skybox widget should show its Night/day title");
        Assert.That(Views.MainMenu.Skybox.AutoProgressionToggle.IsPresent(), Is.True,
            "Skybox widget should contain the Auto day-cycle toggle");
        Assert.That(Views.MainMenu.Skybox.TimeSlider.IsPresent(), Is.True,
            "Skybox widget should contain the time-of-day slider");
        Assert.That(TimeSpan.TryParse(Views.MainMenu.Skybox.TimeLabel.GetText(), out _), Is.True,
            "Skybox widget should show a HH:mm time of day");
        Reporter.Log("Skybox widget shows title, auto toggle, slider and time label");

        PressEscape(delay: 0);
        Views.MainMenu.Skybox.WaitForGone();
    }

    [Test]
    public void TestModifySkyboxTime()
    {
        OpenSkyboxMenu();

        // The slider only reacts while auto progression is off (interactable gating).
        var autoProgressionWasOn = Views.MainMenu.Skybox.IsAutoProgressionOn();
        Views.MainMenu.Skybox.SetAutoProgression(false);
        var timeBefore = Views.MainMenu.Skybox.TimeLabel.GetText();

        Views.MainMenu.Skybox.SetTimeNormalized(0.75f); // 0.75 of a 24h day = 18:00
        var timeAfter = Views.MainMenu.Skybox.TimeLabel.GetText();
        Reporter.Log($"Skybox time changed from {timeBefore} to {timeAfter}");

        Assert.That(TimeSpan.TryParse(timeAfter, out var parsed), Is.True,
            $"Time label should stay in HH:mm format, got '{timeAfter}'");
        // The label rounds the normalized value (observed "17:59" for exactly 0.75).
        Assert.That(parsed, Is.InRange(new TimeSpan(17, 50, 0), new TimeSpan(18, 10, 0)),
            "Time label should reflect the slider position (~18:00 for 0.75)");

        // Cleanup: put auto progression back the way this client started, rather than forcing
        // it on. The InWorld chassis launches the Explorer with --skybox-time-enabled false,
        // which leaves the day cycle off and the toggle unable to latch on — forcing true
        // there fails the test on a cleanup step whose assertions have all already passed.
        // The menu occasionally closes itself right after a slider write — reopen if needed.
        if (!Views.MainMenu.Skybox.IsPresent())
        {
            Reporter.Log("Skybox menu closed itself after the slider write — reopening for cleanup");
            OpenSkyboxMenu();
        }

        Views.MainMenu.Skybox.SetAutoProgression(autoProgressionWasOn);
        PressEscape(delay: 0);
        Views.MainMenu.Skybox.WaitForGone();
    }

    private void OpenSkyboxMenu()
    {
        Views.MainMenu.SkyboxButton.Click();
        Views.MainMenu.Skybox.WaitFor();
        // The widget's show animation eats clicks right after it becomes findable (same
        // pattern as the help menu); there is no raycaster signal here, so fixed wait.
        Wait(1);
    }
}
