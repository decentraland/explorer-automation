using System.Reflection;
using System.Text.RegularExpressions;

namespace ExplorerAutomation.Tests.Common;

internal static class DriverSession
{
    private static string _firstFailure;
    private static readonly Regex TERMINAL_RESULT = new(
        @"(?:^|\n)\s*(?:---->\s*)?(?:(?:SetUp|TearDown|OneTimeSetUp)\s*:\s*)?(?:AltTester\.AltTesterSDK\.Driver\.)?(?:CommandResponseTimeoutException\s*:|AltException\s*:\s*Driver disconnected\s*(?:\r?$))",
        RegexOptions.Multiline | RegexOptions.CultureInvariant);

    internal static string FirstFailure => Volatile.Read(ref _firstFailure);
    internal static bool IsLost => FirstFailure != null;

    internal static void Reset() => Interlocked.Exchange(ref _firstFailure, null);

    internal static Exception Unwrap(Exception exception)
    {
        while (exception is TargetInvocationException { InnerException: not null })
            exception = exception.InnerException;
        return exception;
    }

    internal static bool Record(Exception exception)
    {
        for (var error = exception; error != null; error = error.InnerException)
        {
            if (error is AggregateException aggregate)
            {
                foreach (var inner in aggregate.InnerExceptions)
                    if (Record(inner)) return true;
            }

            if (error is CommandResponseTimeoutException
                || error is AltException { Message: "Driver disconnected" })
            {
                Interlocked.CompareExchange(ref _firstFailure,
                    $"{error.GetType().Name}: {error.Message}", null);
                return true;
            }
        }
        return false;
    }

    internal static bool RecordResult(string message)
    {
        if (message != null && TERMINAL_RESULT.Match(message) is { Success: true } match)
            Interlocked.CompareExchange(ref _firstFailure, match.Value.Trim(), null);
        return IsLost;
    }

    internal static bool CheckCurrentResult()
    {
        if (TestContext.CurrentContext.Result.Outcome.Status == NUnit.Framework.Interfaces.TestStatus.Failed)
            RecordResult(TestContext.CurrentContext.Result.Message);
        return IsLost;
    }

    internal static void SkipIfLost()
    {
        if (IsLost)
            Assert.Ignore($"Blocked by the earlier AltTester session failure: {FirstFailure}");
    }
}
