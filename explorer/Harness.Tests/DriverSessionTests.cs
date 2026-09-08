using System.Reflection;
using AltTester.AltTesterSDK.Driver;
using Allure.NUnit;
using ExplorerAutomation.Tests;
using ExplorerAutomation.Tests.Common;
using ExplorerAutomation.Tests.Tests;
using NUnit.Framework;

namespace ExplorerAutomation.Harness.Tests;

[AllureNUnit]
[NonParallelizable]
public class DriverSessionTests
{
    [SetUp]
    public void ResetSession() => DriverSession.Reset();

    [TearDown]
    public void ClearSession() => DriverSession.Reset();

    [Test]
    public void WrappedTimeoutLatchesSessionFailure()
    {
        var timeout = new CommandResponseTimeoutException();
        Assert.That(DriverSession.Record(new TargetInvocationException(new TargetInvocationException(timeout))), Is.True);
        Assert.That(DriverSession.IsLost, Is.True);
        Assert.That(DriverSession.FirstFailure, Does.StartWith(nameof(CommandResponseTimeoutException)));
    }

    [Test]
    public void DisconnectDoesNotReplaceOriginalTimeout()
    {
        DriverSession.Record(new CommandResponseTimeoutException());
        var first = DriverSession.FirstFailure;
        DriverSession.Record(new AltException("Driver disconnected"));
        Assert.That(DriverSession.FirstFailure, Is.EqualTo(first));
    }

    [Test]
    public void AggregateDisconnectIsTerminal()
    {
        Assert.That(DriverSession.Record(new AggregateException(new InvalidOperationException(),
            new TargetInvocationException(new AltException("Driver disconnected")))), Is.True);
    }

    [Test]
    public void WrappedAggregateExaminesEveryInnerException()
    {
        var errors = new AggregateException(new InvalidOperationException(), new CommandResponseTimeoutException());
        Assert.That(DriverSession.Record(new TargetInvocationException(errors)), Is.True);
    }

    [TestCase("Object not found")]
    [TestCase("Driver disconnected unexpectedly in test data")]
    public void OtherAltErrorsDoNotPoisonSession(string message)
    {
        Assert.That(DriverSession.Record(new AltException(message)), Is.False);
        Assert.That(DriverSession.IsLost, Is.False);
    }

    [Test]
    public void MissingElementAndUiWaitAreNotTransportFailures()
    {
        Assert.That(DriverSession.Record(new NotFoundException()), Is.False);
        Assert.That(DriverSession.Record(new WaitTimeOutException()), Is.False);
        Assert.That(DriverSession.Record(new TargetInvocationException(new AssertionException("No row"))), Is.False);
    }

    [TestCase("SetUp : AltTester.AltTesterSDK.Driver.CommandResponseTimeoutException : timed out")]
    [TestCase("TearDown : AltTester.AltTesterSDK.Driver.AltException : Driver disconnected")]
    [TestCase("SetUp : System.Reflection.TargetInvocationException : outer\n  ----> AltTester.AltTesterSDK.Driver.AltException : Driver disconnected")]
    [TestCase("  ----> AltTester.AltTesterSDK.Driver.AltException : \r\nDriver disconnected\r\n at method()")]
    [TestCase("  ----> AltTester.AltTesterSDK.Driver.CommandResponseTimeoutException : timed out")]
    public void NUnitResultsRecognizeTerminalFailures(string result)
    {
        Assert.That(DriverSession.RecordResult(result), Is.True);
    }

    [TestCase("NUnit.Framework.AssertionException : expected text Driver disconnected")]
    [TestCase("AltTester.AltTesterSDK.Driver.NotFoundException : object missing")]
    [TestCase(null)]
    public void NormalNUnitResultsKeepSessionAvailable(string result)
    {
        Assert.That(DriverSession.RecordResult(result), Is.False);
    }

    [Test]
    public void LostSessionSkipsSetupAndDoesNotInvokeChatCleanupOrScreenshots()
    {
        CommonStuff.AltDriver = null;
        DriverSession.Record(new AltException("Driver disconnected"));
        var fixture = new ChatTests();
        Assert.DoesNotThrow(fixture.OneTimeSetUp);
        var failure = Assert.Catch(fixture.SetUp);
        Assert.That(failure, Is.TypeOf<IgnoreException>(), "NUnit must receive IgnoreException directly, outside the Allure wrapper");
        Assert.DoesNotThrow(fixture.NormalizeChatState);
        Assert.DoesNotThrow(fixture.TearDown);
        Assert.DoesNotThrow(() => Reporter.TakeScreenshot("unavailable"));
    }

    [Test]
    public void ResetAllowsANewDriverSession()
    {
        DriverSession.Record(new AltException("Driver disconnected"));
        DriverSession.Reset();
        Assert.That(DriverSession.IsLost, Is.False);
        Assert.DoesNotThrow(DriverSession.SkipIfLost);
    }
    [Test]
    public void SkippedTestDoesNotReusePreviousPerformanceCapture()
    {
        var fixture = new ViewSignalSmokeTests();
        var field = typeof(BaseTest).GetField("_testPerformance", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var root = Path.Combine(Path.GetTempPath(), "skipped-perf-" + Guid.NewGuid().ToString("N"));
        var capture = new PerformanceCapture(root, "previous test");
        field.SetValue(fixture, capture);
        var metadata = File.ReadAllText(capture.MetadataPath);
        DriverSession.Record(new CommandResponseTimeoutException());
        Assert.Throws<IgnoreException>(fixture.SetUp);
        Assert.That(field.GetValue(fixture), Is.Null);
        Assert.That(File.ReadAllText(capture.MetadataPath), Is.EqualTo(metadata));
    }

}
