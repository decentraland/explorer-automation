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
        // TMP glyph atlases continue resolving after SceneReady (atlas generation runs
        // async even on a primed scene), so sleep a beat before snapshotting to avoid
        // capturing half-rasterised text.
        Thread.Sleep(2000);

        SceneCensus.Log("text-shape pre-snapshot");
        Snapshot.AssertMatchesBaseline("default", tolerance: 1);
    }
}
