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
        Snapshot.AssertMatchesBaseline();
    }
}
