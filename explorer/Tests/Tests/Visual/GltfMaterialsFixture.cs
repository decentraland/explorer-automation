namespace ExplorerAutomation.Tests.Tests.Visual;

[TestFixture]
[AllureNUnit]
[AllureSuite("Visual")]
[AllureSubSuite("gltf-materials")]
[Category("Visual")]
public class GltfMaterialsFixture
{
    [OneTimeSetUp]
    public void LoadScene()
    {
        VisualHost.Load("gltf-materials");
    }

    [Test]
    public void Default()
    {
        Frame.WaitForStable();
        Snapshot.AssertMatchesBaseline("default", tolerance: 0.5);
    }
}
