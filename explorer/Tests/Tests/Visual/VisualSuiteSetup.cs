namespace ExplorerAutomation.Tests.Tests.Visual;

/// <summary>
/// Suite-level lifecycle for visual fixtures. NUnit's [SetUpFixture] is scoped to the
/// namespace it lives in, so this only runs when at least one test under
/// ExplorerAutomation.Tests.Tests.Visual is selected — auth/inworld runs are unaffected.
///
/// Today the host-server lifecycle is owned by metaforge (`mf explorer server start/stop`),
/// not this fixture. All we do here is fail fast with a clear message when a visual run was
/// invoked without the orchestration that injects VISUAL_HOST_URL.
/// </summary>
[SetUpFixture]
public class VisualSuiteSetup
{
    [OneTimeSetUp]
    public void RequireHost()
    {
        var url = Environment.GetEnvironmentVariable("VISUAL_HOST_URL");
        if (!string.IsNullOrWhiteSpace(url))
        {
            Reporter.Log($"VisualSuiteSetup: host = {url}");
            return;
        }

        throw new InvalidOperationException(
            "Visual tests require a running host server but VISUAL_HOST_URL is not set.\n\n" +
            "Run them via metaforge so it can resolve the server and inject the env:\n" +
            "  metaforge explorer server start\n" +
            "  metaforge explorer test --filter \"Category=Visual\"\n\n" +
            "If you're invoking dotnet test directly, set VISUAL_HOST_URL yourself first.");
    }
}
