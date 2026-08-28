namespace ExplorerAutomation.Tests.Tests;

[TestFixture]
[Category("InWorld")]
public class ViewSignalSmokeTests : BaseTest
{
    [Test]
    [Description("The running build carries the view probe and reports the explore panel's lifecycle.")]
    public void ExplorePanelReportsShownThenHidden()
    {
        Assert.That(ViewSignal.GetState("ExplorePanelView"), Is.EqualTo("Hidden").Or.EqualTo("Unknown"));

        Views.MainMenu.BackpackButton.Click();
        ViewSignal.WaitForShown("ExplorePanelView", 40);

        Views.ExplorePanel.CloseButton.Click();
        ViewSignal.WaitForHidden("ExplorePanelView", 40);
    }

    [Test]
    [Description("A view object with a ViewName waits on the signal, not on object presence.")]
    public void ExplorePanelViewObjectUsesTheSignal()
    {
        Views.MainMenu.BackpackButton.Click();
        Views.ExplorePanel.WaitFor(40);
        Assert.That(ViewSignal.IsShown("ExplorePanelView"), Is.True);

        Views.ExplorePanel.CloseButton.Click();
        Views.ExplorePanel.WaitForGone(40);
        Assert.That(Views.ExplorePanel.IsPresent(), Is.False);
    }
}
