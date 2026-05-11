namespace ExplorerAutomation.Tests.Tests.Visual;

[TestFixture]
[AllureNUnit]
[AllureSuite("Visual")]
[AllureSubSuite("avatar-shape")]
[Category("Visual")]
public class AvatarShapeFixture
{
    [OneTimeSetUp]
    public void LoadScene()
    {
        VisualHost.Load("avatar-shape");
    }

    [Test]
    public void Default()
    {
        Frame.WaitForStable();
        Snapshot.AssertMatchesBaseline("default", tolerance: 0.5);
    }
}
