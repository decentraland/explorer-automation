using System.Reflection;
using System.Runtime.CompilerServices;
using AltTester.AltTesterSDK.Driver;
using AltTester.AltTesterSDK.Driver.Commands;
using AltTester.AltTesterSDK.Driver.Notifications;
using Allure.NUnit;
using ExplorerAutomation.Tests;
using ExplorerAutomation.Tests.Common;
using NUnit.Framework;

namespace ExplorerAutomation.Harness.Tests;

[AllureNUnit]
[NonParallelizable]
public class UnityLogNotificationTests
{
    private string _previousSetting;
    private AltDriver _previousDriver;

    [SetUp]
    public void SaveState()
    {
        _previousSetting = Environment.GetEnvironmentVariable("UNITY_LOG_NOTIFICATIONS");
        _previousDriver = CommonStuff.AltDriver;
    }

    [TearDown]
    public void RestoreState()
    {
        Environment.SetEnvironmentVariable("UNITY_LOG_NOTIFICATIONS", _previousSetting);
        CommonStuff.AltDriver = _previousDriver;
    }

    [TestCase(null, false)]
    [TestCase("", false)]
    [TestCase("false", false)]
    [TestCase("0", false)]
    [TestCase("unexpected", false)]
    [TestCase("true", true)]
    [TestCase("TRUE", true)]
    public void OnlyExplicitOptInRegistersUnityLogForwarding(string setting, bool enabled)
    {
        Environment.SetEnvironmentVariable("UNITY_LOG_NOTIFICATIONS", setting);
        var communication = DispatchProxy.Create<IDriverCommunication, Transport>();
        var transport = (Transport)communication;
        var driver = (AltDriver)RuntimeHelpers.GetUninitializedObject(typeof(AltDriver));
        typeof(AltDriver).GetField("communicationHandler", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(driver, communication);
        CommonStuff.AltDriver = driver;

        Reporter.SetupUnityLogListener();

        Assert.That(transport.ListenerRegistrations, Is.EqualTo(enabled ? 1 : 0));
        Assert.That(transport.Commands, Has.Count.EqualTo(enabled ? 1 : 0));
        if (enabled)
        {
            Assert.That(transport.Commands[0], Is.TypeOf<ActivateNotification>());
            Assert.That(((ActivateNotification)transport.Commands[0]).NotificationType, Is.EqualTo(NotificationType.LOG));
        }
    }

    public class Transport : DispatchProxy
    {
        public int ListenerRegistrations;
        public readonly List<CommandParams> Commands = new();

        protected override object Invoke(MethodInfo method, object[] args)
        {
            if (method.Name == "AddNotificationListener") ListenerRegistrations++;
            else if (method.Name == "Send") Commands.Add((CommandParams)args[0]);
            else if (method.Name == "Recvall") return "Ok";
            else if (method.Name == "GetDelayAfterCommand") return 0f;
            else throw new InvalidOperationException($"Unexpected transport call: {method.Name}");
            return null;
        }
    }
}
