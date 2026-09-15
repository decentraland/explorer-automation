using ExplorerAutomation.Tests.Common.SyntheticInput;

namespace ExplorerAutomation.Tests.Tests;

/// <summary>
/// Demo fixture for the synthetic input layer: the client is driven through the production input
/// pipelines (<see cref="WorldInput"/>) rather than through the UI hierarchy, against the SDK7 test
/// scenes world on the zone environment.
///
/// Runs in its own category because it needs a client launched on zone with an identity created
/// there — the InWorld chassis boots on org:
/// <code>
///   mf account create synthetic-input --env zone &amp;&amp; mf account login synthetic-input --env zone
///   mf explorer run -- --alttester --dclenv zone --realm sdk7testscenes.dcl.eth --position 0,0
///   mf explorer test --filter "Category=SyntheticInput"
/// </code>
/// The realm and position arguments only save a realm change: the fixture navigates to the world and
/// parcel itself when the client is anywhere else on zone.
/// </summary>
[AllureSuite("Synthetic Input Tests")]
[Category("SyntheticInput")]
[Order(30)]
public class SyntheticInputTests : BaseTest
{
    private const string REQUIRED_ENVIRONMENT = "zone";
    private const string WORLD = "sdk7testscenes.dcl.eth";
    private static readonly Parcel SPAWN_PARCEL = new(0, 0);

    private const double LEG_SECONDS = 2;

    // A jog covers several meters in two seconds; one meter is enough to prove the hold moved the
    // avatar while leaving room for a collider that shortens a leg.
    private const double MIN_LEG_DISTANCE = 1.0;

    protected override void EnsureInWorld()
    {
        base.EnsureInWorld();

        if (!WorldInput.IsReady() || !Navigation.IsReady())
            throw new AssertionException("The synthetic input layer is not installed — launch the client with --alttester (or --mcp).");

        // Only the environment gate below can skip; a realm change on the wrong environment would still
        // land on a world of that environment and every test would then be exercising the wrong backend.
        if (Navigation.Status().Environment == REQUIRED_ENVIRONMENT)
            Navigation.EnsureWorld(WORLD, SPAWN_PARCEL);
    }

    [SetUp]
    public void RequireTargetEnvironment()
    {
        var environment = Navigation.Status().Environment;

        if (environment != REQUIRED_ENVIRONMENT)
            Assert.Ignore($"Client is on '{environment}', this fixture needs '{REQUIRED_ENVIRONMENT}' — launch with --dclenv {REQUIRED_ENVIRONMENT} and an identity created with `mf account create <name> --env {REQUIRED_ENVIRONMENT}`");
    }

    [Test]
    public void TestWalkAroundTheScene()
    {
        var start = WorldInput.PlayerState();
        Reporter.Log($"Start: {start}");

        var status = Navigation.Status();
        Assert.That(status.IsOnWorld(WORLD), Is.True, $"Expected to stand on {WORLD}, but the realm is '{status.RealmName}'");
        Assert.That(status.SceneReady, Is.True, $"The scene at {status.CurrentParcel} should be ready before walking");

        // A square: forward, right, back, left. Each leg goes through the real locomotion pipeline.
        AssertLegMoved(WorldInput.Walk(0, 1, LEG_SECONDS), "forward");
        AssertLegMoved(WorldInput.Walk(1, 0, LEG_SECONDS), "right");
        AssertLegMoved(WorldInput.Walk(0, -1, LEG_SECONDS), "back");
        AssertLegMoved(WorldInput.Walk(-1, 0, LEG_SECONDS), "left");

        var end = WorldInput.PlayerState();
        Reporter.Log($"End: {end}, {start.Position.FlatDistanceTo(end.Position):F2}m from the start");

        Assert.That(end.IsGrounded, Is.True, "The avatar should be back on the ground after the last leg");
        Assert.That(Navigation.Status().IsOnWorld(WORLD), Is.True, "Walking must not have left the world");
    }

    private static void AssertLegMoved(WalkResult leg, string name)
    {
        Assert.That(leg.Completed, Is.True, $"The {name} leg should run to its full duration, got {leg.Delivery}");
        Assert.That(leg.Start.FlatDistanceTo(leg.End), Is.GreaterThanOrEqualTo(MIN_LEG_DISTANCE),
            $"The {name} leg should move the avatar at least {MIN_LEG_DISTANCE}m, moved {leg.Distance:F2}m");
    }
}
