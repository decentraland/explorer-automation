namespace ExplorerAutomation.Tests.Tests.Visual;

[TestFixture]
[AllureNUnit]
[AllureSuite("Visual")]
[AllureSubSuite("materials")]
[Category("Visual")]
[Order(22)]
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
        Snapshot.AssertMatchesBaseline("default", tolerance: 1);
    }
}
