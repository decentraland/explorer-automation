namespace ExplorerAutomation.Tests.Tests.Visual;

[TestFixture]
[AllureNUnit]
[AllureSuite("Visual")]
[AllureSubSuite("animations")]
[Category("Visual")]
public class AnimationsFixture
{
    [OneTimeSetUp]
    public void LoadScene()
    {
        VisualHost.Load("animations");
    }

    [Test]
    public void Default()
    {
        // Col 4 has the longest timeline: it opens (~1.25 s), waits until
        // scene-time ≈1.5 s, then fires Close (~1.25 s) — done by ≈2.75 s.
        // Sleep well past that so all five columns are settled before the
        // snapshot fires.
        Thread.Sleep(2500);

        Snapshot.AssertMatchesBaseline("default", tolerance: 0.5);
    }
}
