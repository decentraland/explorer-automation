namespace ExplorerAutomation.Tests.Tests.Visual;

[TestFixture]
[AllureNUnit]
[AllureSuite("Visual")]
[AllureSubSuite("tweens")]
[Category("Visual")]
public class TweensFixture
{
    [OneTimeSetUp]
    public void LoadScene()
    {
        VisualHost.Load("tweens");
    }

    [Test]
    public void Default()
    {
        // Every tween in the scene uses TWEEN_DURATION = 50 ms so the visible
        // motion is over within ~100 ms. Three columns (Col 0 yellow callback,
        // Col 1 green sequence-complete, Col 5 material-UV callback) run an
        // extra post-completion system that needs at least one more frame to
        // snap the entity to its final pose and recolor. A short sleep gives
        // those follow-up systems plenty of time before the snapshot fires.
        Thread.Sleep(1000);

        Snapshot.AssertMatchesBaseline("default", tolerance: 1);
    }
}
