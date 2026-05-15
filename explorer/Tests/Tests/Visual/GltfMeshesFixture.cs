namespace ExplorerAutomation.Tests.Tests.Visual;

[TestFixture]
[AllureNUnit]
[AllureSuite("Visual")]
[AllureSubSuite("gltf-meshes")]
[Category("Visual")]
public class GltfMeshesFixture
{
    [OneTimeSetUp]
    public void LoadScene()
    {
        VisualHost.Load("gltf-meshes");
    }

    [Test]
    public void Default()
    {
        Frame.WaitForStable();
        Snapshot.AssertMatchesBaseline("default", tolerance: 0.5);
    }
}
