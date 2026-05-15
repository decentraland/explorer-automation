namespace ExplorerAutomation.Tests.Tests.Visual;

[TestFixture]
[AllureNUnit]
[AllureSuite("Visual")]
[AllureSubSuite("gltf")]
[Category("Visual")]
[Order(21)]
public class GltfFixture
{
    [OneTimeSetUp]
    public void LoadScene()
    {
        VisualHost.Load("gltf");
    }

    [Test]
    public void Default()
    {
        SceneReady.WaitUntilReady();
        Snapshot.AssertMatchesBaseline("default", tolerance: 3);
    }
}
