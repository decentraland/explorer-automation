namespace ExplorerAutomation.Tests.Tests.Visual;

[TestFixture]
[AllureNUnit]
[AllureSuite("Visual")]
[AllureSubSuite("core")]
[Category("Visual")]
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
        Frame.WaitForStable();
        Snapshot.AssertMatchesBaseline("default", tolerance: 3);
    }
}
