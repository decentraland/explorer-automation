namespace ExplorerAutomation.Tests.Tests.Visual;

[TestFixture]
[AllureNUnit]
[AllureSuite("Visual")]
[AllureSubSuite("swap_material")]
[Category("Visual")]
public class SwapMaterialFixture
{
    [OneTimeSetUp]
    public void LoadScene()
    {
        VisualHost.Load("swap_material");
    }

    [Test]
    public void Default()
    {
        Frame.WaitForStable();
        Snapshot.AssertMatchesBaseline("default", tolerance: 0.5);
    }
}
