using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

namespace MetaForge.TestLogger;

/// <summary>
/// A vstest logger that emits structured progress lines for the MetaForge CLI to parse.
/// </summary>
[FriendlyName("MetaForge")]
[ExtensionUri("logger://metaforge")]
public class MetaForgeTestLogger : ITestLoggerWithParameters
{
    private int _completed;

    public void Initialize(TestLoggerEvents events, string testRunDirectory)
    {
        events.TestResult += OnTestResult;
    }

    public void Initialize(TestLoggerEvents events, Dictionary<string, string> parameters)
    {
        events.TestResult += OnTestResult;
    }

    private void OnTestResult(object sender, TestResultEventArgs e)
    {
        var count = Interlocked.Increment(ref _completed);
        var outcome = e.Result.Outcome switch
        {
            TestOutcome.Passed => "Passed",
            TestOutcome.Failed => "Failed",
            TestOutcome.Skipped => "Skipped",
            _ => "Unknown"
        };
        var name = EscapeJson(e.Result.TestCase.DisplayName);

        Console.Out.WriteLine($"##mf##{{\"c\":{count},\"o\":\"{outcome}\",\"n\":\"{name}\"}}");
        Console.Out.Flush();
    }

    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
    }
}
