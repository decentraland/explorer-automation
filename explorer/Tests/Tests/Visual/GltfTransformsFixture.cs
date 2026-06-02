namespace ExplorerAutomation.Tests.Tests.Visual;

[TestFixture]
[AllureNUnit]
[AllureSuite("Visual")]
[AllureSubSuite("gltf-transforms")]
[Category("Visual")]
public class GltfTransformsFixture
{
    [OneTimeSetUp]
    public void LoadScene()
    {
        VisualHost.Load("gltf-transforms");
    }

    [Test]
    public void Default()
    {
        Snapshot.AssertMatchesBaseline("default", tolerance: 0.5);
    }
}
