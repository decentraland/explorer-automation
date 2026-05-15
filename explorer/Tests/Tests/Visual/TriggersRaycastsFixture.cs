namespace ExplorerAutomation.Tests.Tests.Visual;

[TestFixture]
[AllureNUnit]
[AllureSuite("Visual")]
[AllureSubSuite("triggers-raycasts")]
[Category("Visual")]
public class TriggersRaycastsFixture
{
    [OneTimeSetUp]
    public void LoadScene()
    {
        VisualHost.Load("triggers-raycasts");
    }

    [Test]
    public void Default()
    {
        Frame.WaitForStable();
        Snapshot.AssertMatchesBaseline("default", tolerance: 0.5);
    }
}
