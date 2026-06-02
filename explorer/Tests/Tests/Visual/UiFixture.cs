namespace ExplorerAutomation.Tests.Tests.Visual;

[TestFixture]
[AllureNUnit]
[AllureSuite("Visual")]
[AllureSubSuite("ui")]
[Category("Visual")]
[Order(23)]
public class UiFixture
{
    [OneTimeSetUp]
    public void LoadScene()
    {
        VisualHost.Load("ui");
    }

    [Test]
    public void Default()
    {
        Snapshot.AssertMatchesBaseline("default", tolerance: 0.5);
    }
}
