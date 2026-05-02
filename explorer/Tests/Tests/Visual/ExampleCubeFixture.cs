namespace ExplorerAutomation.Tests.Tests.Visual;

[TestFixture]
[AllureNUnit]
[AllureSuite("Visual")]
[AllureSubSuite("example-cube")]
[Category("Visual")]
public class ExampleCubeFixture
{
    [OneTimeSetUp]
    public void LoadScene()
    {
        VisualHost.Load("example-cube");
    }

    [Test]
    public void Default()
    {
        Frame.WaitForStable();
        Snapshot.AssertMatchesBaseline("default", tolerance: 0.5);
    }
}
