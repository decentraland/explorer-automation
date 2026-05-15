namespace ExplorerAutomation.Tests.Tests.Visual;

[TestFixture]
[AllureNUnit]
[AllureSuite("Visual")]
[AllureSubSuite("swap_material")]
[Category("Visual")]
public class SwapMaterialFixture
{
    [OneTimeSetUp]
    public void LoadScene()
    {
        VisualHost.Load("swap_material");
    }

    [Test]
    public void Default()
    {
        // GltfNodeModifiers run after the GLB finishes loading; material/texture
        // overrides (lava .jpg, smoke-puff .png) only resolve once their assets
        // are decoded. A couple of seconds covers GLB + texture loads with
        // generous grace before Frame.WaitForStable starts sampling.
        Thread.Sleep(1000);

        Frame.WaitForStable();
        Snapshot.AssertMatchesBaseline("default", tolerance: 0.5);
    }
}
