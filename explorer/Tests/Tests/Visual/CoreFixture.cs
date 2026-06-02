namespace ExplorerAutomation.Tests.Tests.Visual;

[TestFixture]
[AllureNUnit]
[AllureSuite("Visual")]
[AllureSubSuite("core")]
[Category("Visual")]
[Order(20)]
public class CoreFixture
{
    [OneTimeSetUp]
    public void LoadScene()
    {
        VisualHost.Load("core");
    }

    [Test]
    public void Default()
    {
        Snapshot.AssertMatchesBaseline("default", tolerance: 0.5);
    }
}
