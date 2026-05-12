namespace ExplorerAutomation.Tests.Tests.Visual;

[TestFixture]
[AllureNUnit]
[AllureSuite("Visual")]
[AllureSubSuite("ui")]
[Category("Visual")]
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
        SceneReady.WaitUntilReady();
        Snapshot.AssertMatchesBaseline("default", tolerance: 3);
    }
}
