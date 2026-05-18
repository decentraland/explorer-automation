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
        // Avatars need time to fetch wearables (incl. NFTs from Ethereum/Polygon
        // collections) and resolve the local emote GLBs in expressionTriggerId.
        // The emote GLBs declare 60 s clips with only 2 keyframes — they're
        // designed as near-stationary podium poses — so a few seconds is enough
        // for the rig to settle into a visually stable region of the curve
        // before the snapshot fires.
        Thread.Sleep(10000);

        Snapshot.AssertMatchesBaseline("default", tolerance: 1.5);
    }
}
