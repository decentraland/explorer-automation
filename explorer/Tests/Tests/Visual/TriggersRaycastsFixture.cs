namespace ExplorerAutomation.Tests.Tests.Visual;

[TestFixture]
[AllureNUnit]
[AllureSuite("Visual")]
[AllureSubSuite("triggers-raycasts")]
[Category("Visual")]
public class TriggersRaycastsFixture
{
    [OneTimeSetUp]
    public void LoadScene()
    {
        VisualHost.Load("triggers-raycasts");
    }

    [Test]
    public void Default()
    {
        // Col 4 (CL_PLAYER trigger) relies on a movePlayerTo retry loop in the
        // scene with RETRY_DEADLINE = 6.0 s — the avatar may take a few seconds
        // to register before the teleport propagates. Sleep past that deadline
        // so the trigger has fired (or the deadline has elapsed, surfacing the
        // regression in the diff) before Frame.WaitForStable starts sampling.
        Thread.Sleep(7000);

        Frame.WaitForStable();
        Snapshot.AssertMatchesBaseline("default", tolerance: 0.5);
    }
}
