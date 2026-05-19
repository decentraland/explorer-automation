namespace ExplorerAutomation.Tests.Tests.Visual;

[TestFixture]
[AllureNUnit]
[AllureSuite("Visual")]
[AllureSubSuite("text-shape")]
[Category("Visual")]
public class TextShapeFixture
{
    [OneTimeSetUp]
    public void LoadScene()
    {
        VisualHost.Load("text-shape");
    }

    [Test]
    public void Default()
    {
        Snapshot.AssertMatchesBaseline("default", tolerance: 0.5);
    }
}
