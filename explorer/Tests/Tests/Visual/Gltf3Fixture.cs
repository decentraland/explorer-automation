namespace ExplorerAutomation.Tests.Tests.Visual;

[TestFixture]
[AllureNUnit]
[AllureSuite("Visual")]
[AllureSubSuite("gltf3")]
[Category("Visual")]
public class Gltf3Fixture
{
    [OneTimeSetUp]
    public void LoadScene()
    {
        VisualHost.Load("gltf3");
    }

    [Test]
    public void Default()
    {
        Frame.WaitForStable();
        Snapshot.AssertMatchesBaseline("default", tolerance: 0.5);
    }
}
