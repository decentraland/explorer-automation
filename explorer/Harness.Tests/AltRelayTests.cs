using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using AltTester.AltTesterSDK.Driver;
using ExplorerAutomation.AltRelay;
using NUnit.Framework;

namespace ExplorerAutomation.Harness.Tests;

/// <summary>The real AltDriver through the relay against a fake instrumented app speaking the AltTester 2.3 wire format.</summary>
[NonParallelizable]
public class AltRelayTests
{
    private const string APP_NAME = "__default__";
    private const string SCENE = "RelayScene";
    private const int CONNECT_TIMEOUT_SECONDS = 5;

    [Test]
    public async Task DriverTalksToTheAppThroughTheRelay()
    {
        using var relay = RelayServer.Start(port: 0);
        using var app = await FakeApp.ConnectAsync(relay.Port, APP_NAME);
        var driver = NewDriver(relay.Port);
        try
        {
            Assert.That(driver.GetCurrentScene(), Is.EqualTo(SCENE));
            Assert.That(app.Commands, Does.Contain("getServerVersion").And.Contain("getCurrentScene"));
        }
        finally
        {
            StopQuietly(driver);
        }
    }

    [Test]
    public async Task AppConnectingAfterTheDriverStillPairs()
    {
        using var relay = RelayServer.Start(port: 0);
        var connecting = Task.Run(() => NewDriver(relay.Port));
        await Task.Delay(500);
        using var app = await FakeApp.ConnectAsync(relay.Port, APP_NAME);
        var driver = await connecting;
        try
        {
            Assert.That(driver.GetCurrentScene(), Is.EqualTo(SCENE));
        }
        finally
        {
            StopQuietly(driver);
        }
    }

    [Test]
    public async Task AppSurvivesADriverRestart()
    {
        using var relay = RelayServer.Start(port: 0);
        using var app = await FakeApp.ConnectAsync(relay.Port, APP_NAME);
        StopQuietly(NewDriver(relay.Port));
        var driver = NewDriver(relay.Port);
        try
        {
            Assert.That(driver.GetCurrentScene(), Is.EqualTo(SCENE));
        }
        finally
        {
            StopQuietly(driver);
        }
    }

    [Test]
    public async Task DriverLosesItsSessionWhenTheAppLeaves()
    {
        using var relay = RelayServer.Start(port: 0);
        var app = await FakeApp.ConnectAsync(relay.Port, APP_NAME);
        var driver = NewDriver(relay.Port);
        try
        {
            driver.SetCommandResponseTimeout(3);
            app.Dispose();
            await Task.Delay(500);
            Assert.Catch<Exception>(() => driver.GetCurrentScene());
        }
        finally
        {
            StopQuietly(driver);
        }
    }

    private static AltDriver NewDriver(int port) =>
        new(host: "127.0.0.1", port: port, appName: APP_NAME, connectTimeout: CONNECT_TIMEOUT_SECONDS);

    private static void StopQuietly(AltDriver driver)
    {
        try { driver.Stop(); }
        catch (Exception) { }
    }

    private sealed class FakeApp : IDisposable
    {
        private readonly ClientWebSocket _socket = new();
        private readonly CancellationTokenSource _cts = new();
        private readonly List<string> _commands = [];

        public IReadOnlyList<string> Commands
        {
            get { lock (_commands) return _commands.ToArray(); }
        }

        public static async Task<FakeApp> ConnectAsync(int port, string appName)
        {
            var app = new FakeApp();
            var uri = new Uri($"ws://127.0.0.1:{port}/altws/app?appName={appName}&platform=harness&platformVersion=0&deviceInstanceId=harness&appId=harness");
            await app._socket.ConnectAsync(uri, CancellationToken.None);
            _ = Task.Run(app.AnswerLoopAsync);
            return app;
        }

        public void Dispose()
        {
            _cts.Cancel();
            _socket.Dispose();
        }

        private async Task AnswerLoopAsync()
        {
            var buffer = new byte[64 * 1024];
            try
            {
                while (!_cts.IsCancellationRequested)
                {
                    using var message = new MemoryStream();
                    WebSocketReceiveResult result;
                    do
                    {
                        result = await _socket.ReceiveAsync(buffer, _cts.Token);
                        if (result.MessageType == WebSocketMessageType.Close) return;
                        message.Write(buffer, 0, result.Count);
                    } while (!result.EndOfMessage);
                    var reply = Answer(Encoding.UTF8.GetString(message.ToArray()));
                    await _socket.SendAsync(Encoding.UTF8.GetBytes(reply), WebSocketMessageType.Text, true, _cts.Token);
                }
            }
            catch (Exception e) when (e is OperationCanceledException or WebSocketException or ObjectDisposedException or IOException) { }
        }

        private string Answer(string request)
        {
            using var command = JsonDocument.Parse(request);
            var name = command.RootElement.GetProperty("commandName").GetString();
            var messageId = command.RootElement.GetProperty("messageId").GetString();
            lock (_commands) _commands.Add(name);
            var data = name switch
            {
                "getServerVersion" => JsonSerializer.Serialize("2.3.0"),
                "getCurrentScene" => JsonSerializer.Serialize(new { name = SCENE }),
                _ => JsonSerializer.Serialize("Ok"),
            };
            return JsonSerializer.Serialize(new { messageId, commandName = name, driverId = "", isNotification = false, data, error = (object)null });
        }
    }
}
