namespace ExplorerAutomation.Tests.Tests;

[AllureSuite("Skybox Tests")]
[Category("InWorld")]
[Order(19)]
public class SkyboxTests : BaseTest
{
    [Test]
    public void TestSkyboxWidgetPresence()
    {
        Views.Minimap.OpenSkybox();

        Assert.That(Views.Minimap.Skybox.TitleLabel.GetText(), Is.EqualTo("Auto"),
            "Skybox widget should label its day-cycle toggle Auto");
        Assert.That(Views.Minimap.Skybox.AutoProgressionToggle.IsPresent(), Is.True,
            "Skybox widget should contain the Auto day-cycle toggle");
        Assert.That(Views.Minimap.Skybox.TimeSlider.IsPresent(), Is.True,
            "Skybox widget should contain the time-of-day slider");
        Assert.That(TimeSpan.TryParse(Views.Minimap.Skybox.ReadTime(), out _), Is.True,
            "Skybox widget should show a HH:mm time of day");
        Reporter.Log("Skybox widget shows title, auto toggle, slider and time label");

        Views.Minimap.CloseSkybox();
    }

    [Test]
    public void TestModifySkyboxTime()
    {
        Views.Minimap.OpenSkybox();

        // The slider only reacts while auto progression is off (interactable gating).
        var autoProgressionWasOn = Views.Minimap.Skybox.IsAutoProgressionOn();
        Views.Minimap.Skybox.SetAutoProgression(false);
        var timeBefore = Views.Minimap.Skybox.ReadTime();

        Views.Minimap.Skybox.SetTimeNormalized(0.75f); // 0.75 of a 24h day = 18:00
        var timeAfter = Views.Minimap.Skybox.ReadTime();
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
        Views.Minimap.Skybox.SetAutoProgression(autoProgressionWasOn);
        Views.Minimap.CloseSkybox();
    }
}
