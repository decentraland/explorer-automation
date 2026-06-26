namespace ExplorerAutomation.Tests.Tests.Visual;

[TestFixture]
[AllureNUnit]
[AllureSuite("Visual")]
[AllureSubSuite("gltf-uvs")]
[Category("Visual")]
public class GltfUvsFixture
{
    [OneTimeSetUp]
    public void LoadScene()
    {
        VisualHost.Load("gltf-uvs");
    }

    [Test]
    public void Default()
    {
        Snapshot.AssertMatchesBaseline();
    }
}
