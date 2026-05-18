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
        // SceneReady gates on the SDK-side probe: scene script done, ECS entities materialized.
        // Without it we're racing the hot-reload — TextShape glyph atlases finish loading later
        // than the scene-loaded signal, so Frame.WaitForStable can latch onto a "stable but
        // incomplete" frame (no text → still pixel-identical between samples).
        SceneReady.WaitUntilReady();
        SceneCensus.Log("text-shape post-ready");

        Frame.WaitForStable();
        SceneCensus.Log("text-shape post-stable");

        Snapshot.AssertMatchesBaseline("default", tolerance: 0.5);
    }
}
