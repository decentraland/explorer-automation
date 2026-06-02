namespace ExplorerAutomation.Tests.Tests.Visual;

[TestFixture]
[AllureNUnit]
[AllureSuite("Visual")]
[AllureSubSuite("lights")]
[Category("Visual")]
public class LightsFixture
{
    [OneTimeSetUp]
    public void LoadScene()
    {
        VisualHost.Load("lights");
    }

    [Test]
    public void Default()
    {
        Snapshot.AssertMatchesBaseline("default", tolerance: 0.5);
    }
}
