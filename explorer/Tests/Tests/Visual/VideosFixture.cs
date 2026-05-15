namespace ExplorerAutomation.Tests.Tests.Visual;

[TestFixture]
[AllureNUnit]
[AllureSuite("Visual")]
[AllureSubSuite("videos")]
[Category("Visual")]
public class VideosFixture
{
    [OneTimeSetUp]
    public void LoadScene()
    {
        VisualHost.Load("videos");
    }

    [Test]
    public void Default()
    {
        Frame.WaitForStable();
        Snapshot.AssertMatchesBaseline("default", tolerance: 0.5);
    }
}
