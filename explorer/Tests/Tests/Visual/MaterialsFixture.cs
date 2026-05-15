namespace ExplorerAutomation.Tests.Tests.Visual;

[TestFixture]
[AllureNUnit]
[AllureSuite("Visual")]
[AllureSubSuite("materials")]
[Category("Visual")]
public class MaterialsFixture
{
    [OneTimeSetUp]
    public void LoadScene()
    {
        VisualHost.Load("materials");
    }

    [Test]
    public void Default()
    {
        Frame.WaitForStable();
        Snapshot.AssertMatchesBaseline("default", tolerance: 3);
    }
}
