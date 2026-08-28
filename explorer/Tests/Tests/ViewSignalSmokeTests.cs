namespace ExplorerAutomation.Tests.Tests;

[TestFixture]
[Category("InWorld")]
public class ViewSignalSmokeTests : BaseTest
{
    [Test]
    [Description("The running build carries the view probe and reports the explore panel's lifecycle.")]
    public void ExplorePanelReportsShownThenHidden()
    {
        Assert.That(ViewSignal.IsAvailable, Is.True,
            "This build does not carry MVC.AltTesterViewProbe. Run against a paired branch build.");

        Assert.That(ViewSignal.GetState("ExplorePanelView"), Is.EqualTo("Hidden").Or.EqualTo("Unknown"));

        Views.MainMenu.BackpackButton.Click();
        ViewSignal.WaitForShown("ExplorePanelView", 40);

        Views.ExplorePanel.CloseButton.Click();
        ViewSignal.WaitForHidden("ExplorePanelView", 40);
    }
}
