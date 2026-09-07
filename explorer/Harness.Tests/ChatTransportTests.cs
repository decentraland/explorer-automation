using System.Reflection;
using System.Runtime.CompilerServices;
using AltTester.AltTesterSDK.Driver;
using AltTester.AltTesterSDK.Driver.Commands;
using Allure.NUnit;
using ExplorerAutomation.Tests;
using ExplorerAutomation.Tests.Common;
using ExplorerAutomation.Tests.Tests;
using ExplorerAutomation.Tests.Views;
using NUnit.Framework;

namespace ExplorerAutomation.Harness.Tests;

[AllureNUnit]
[NonParallelizable]
public class ChatTransportTests
{
    [SetUp]
    public void ResetSession() => DriverSession.Reset();

    [TearDown]
    public void ClearSession()
    {
        CommonStuff.AltDriver = null;
        DriverSession.Reset();
    }

    [Test]
    public void PopupDismissalTimeoutIsPreservedWithoutRetryingTheReaction()
    {
        var transport = InstallTransport();
        var failure = Assert.Catch(() => new ChatPanelView().ReactToOwnMessage("injected message"));
        Assert.That(transport.ReactionRowWasFound, Is.True, failure?.ToString());
        Assert.That(DriverSession.Unwrap(failure), Is.SameAs(transport.Timeout));
        Assert.That(transport.MessageLookups, Is.EqualTo(1), "A transport failure must not repeat the reaction");
    }

    [Test]
    public void InitialBootstrapTransportFailureFailsInsteadOfSkippingTheWholeRun()
    {
        var transport = InstallTransport();
        transport.FailImmediately = true;
        var failure = Assert.Catch(new ChatTests().OneTimeSetUp);
        Assert.That(DriverSession.Unwrap(failure), Is.SameAs(transport.Timeout));
        Assert.That(DriverSession.IsLost, Is.True);
    }

    private static Transport InstallTransport()
    {
        var communication = DispatchProxy.Create<IDriverCommunication, Transport>();
        var driver = (AltDriver)RuntimeHelpers.GetUninitializedObject(typeof(AltDriver));
        typeof(AltDriver).GetField("communicationHandler", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(driver, communication);
        CommonStuff.AltDriver = driver;
        return (Transport)communication;
    }

    // Replace only the network boundary; execute the real POM, Allure aspects and SDK commands.
    public class Transport : DispatchProxy
    {
        public readonly CommandResponseTimeoutException Timeout = new();
        public bool FailImmediately;
        public bool ReactionRowWasFound;
        public int MessageLookups;

        protected override object Invoke(MethodInfo method, object[] args)
        {
            if (method.Name == "Send")
            {
                if (FailImmediately) throw Timeout;
                var command = (CommandParams)args[0];
                if (command.commandName == "findObjects") MessageLookups++;
                if (command is BaseFindObjectsParams find)
                {
                    if (ReactionRowWasFound && find.path.Contains("ChatMessageReactionSelector"))
                        throw Timeout;
                    if (find.path.Contains("ChatReactionsRow_Own")) ReactionRowWasFound = true;
                }
                return null;
            }
            if (method.Name == "Recvall")
            {
                var result = method.GetGenericArguments()[0];
                if (result == typeof(AltObject)) return new AltObject("injected object");
                if (result == typeof(List<AltObject>)) return new List<AltObject> { new("injected message") };
                if (result == typeof(string))
                    return ((CommandParams)args[0]).commandName == "getText" ? "injected message" : "Finished";
                throw new InvalidOperationException($"Unexpected SDK response type: {result}");
            }
            if (method.Name == "GetImplicitTimeout") return -1f;
            return method.ReturnType == typeof(float) ? 0f : null;
        }
    }
}
