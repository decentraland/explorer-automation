namespace ExplorerAutomation.Tests.Tests.Visual;

[TestFixture]
[AllureNUnit]
[AllureSuite("Visual")]
[AllureSubSuite("gltf2")]
[Category("Visual")]
public class Gltf2Fixture
{
    [OneTimeSetUp]
    public void LoadScene()
    {
        VisualHost.Load("gltf2");
    }

    [Test]
    public void Default()
    {
        Frame.WaitForStable();
        Snapshot.AssertMatchesBaseline("default", tolerance: 0.5);
    }
}
