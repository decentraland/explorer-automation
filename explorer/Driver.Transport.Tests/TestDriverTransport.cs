using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AltTester.AltTesterSDK.Driver.Commands;
using AltTester.AltTesterSDK.Driver.Communication;
using Newtonsoft.Json;
using NUnit.Framework;

namespace AltTester.AltTesterSDK.Driver.Tests
{
    [TestFixture, NonParallelizable]
    public class TestDriverTransport
    {
        private static DriverCommunicationHandler Handler(int port, int timeout = 2) =>
            new DriverCommunicationHandler("127.0.0.1", port, timeout, "test", "", "", "", "");

        private static DriverWebSocketClient Client(DriverCommunicationHandler handler) =>
            (DriverWebSocketClient)typeof(DriverCommunicationHandler)
                .GetField("wsClient", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(handler);

        [Test]
        public void RegisteredConnectionReceivesResponse()
        {
            using var server = new LoopbackServer();
            var handler = Handler(server.Port);
            try
            {
                handler.Connect();
                var command = new CommandParams("echo", null);
                handler.Send(command);
                Assert.That(handler.Recvall<string>(command), Is.EqualTo("ok"));
            }
            finally { handler.Close(); }
        }

        [Test]
        public void OpenSocketWithoutRegistrationMustFailWithinConnectBudget()
        {
            using var server = new LoopbackServer(register: false);
            var handler = Handler(server.Port, 1);
            var elapsed = Stopwatch.StartNew();
            try
            {
                Assert.Throws<ConnectionTimeoutException>(() => handler.Connect());
                Assert.That(elapsed.Elapsed.TotalSeconds, Is.LessThan(3));
            }
            finally { handler.Close(); }
        }

        [Test]
        public void SendAfterCloseFailsInsteadOfSilentlyDroppingCommand()
        {
            using var server = new LoopbackServer();
            var handler = Handler(server.Port);
            handler.Connect();
            handler.Close();
            Assert.Throws<AltException>(() => handler.Send(new CommandParams("echo", null)));
        }

        [Test]
        public void SuccessfulAbnormalCloseReconnectDoesNotPoisonReplacement()
        {
            using var server = new LoopbackServer();
            var handler = Handler(server.Port);
            using var callbackFinished = new ManualResetEventSlim();
            try
            {
                handler.Connect();
                var oldClient = Client(handler);
                oldClient.OnCloseEvent += (_, __) => callbackFinished.Set();
                server.GetPeer(0).Abort();
                Assert.That(callbackFinished.Wait(TimeSpan.FromSeconds(8)), Is.True, "close callback did not finish");
                Assert.That(Client(handler), Is.Not.SameAs(oldClient));
                Assert.That(Client(handler).DriverRegisteredCalled, Is.True);
                var command = new CommandParams("echo", null);
                handler.Send(command);
                Assert.That(handler.Recvall<string>(command), Is.EqualTo("ok"));
            }
            finally { handler.Close(); }
        }

        [Test]
        public void FailedReconnectLeavesSessionClosedWithoutRecursiveRetry()
        {
            using var server = new LoopbackServer(registerReplacement: false, abortUnregistered: true);
            var handler = Handler(server.Port, 1);
            using var callbackFinished = new ManualResetEventSlim();
            try
            {
                handler.Connect();
                Client(handler).OnCloseEvent += (_, __) => callbackFinished.Set();
                server.GetPeer(0).Abort();
                Assert.That(callbackFinished.Wait(TimeSpan.FromSeconds(4)), Is.True);
                Assert.Throws<AltException>(() => handler.Send(new CommandParams("echo", null)));
                Assert.That(server.AcceptedCount, Is.InRange(2, 30));
            }
            finally { handler.Close(); }
        }

        [Test]
        public void ExplicitCloseDuringReconnectDoesNotResurrectSession()
        {
            using var server = new LoopbackServer(registerReplacement: false);
            var handler = Handler(server.Port, 2);
            using var callbackFinished = new ManualResetEventSlim();
            try
            {
                handler.Connect();
                Client(handler).OnCloseEvent += (_, __) => callbackFinished.Set();
                server.GetPeer(0).Abort();
                server.GetPeer(1);
                handler.Close();
                Assert.That(callbackFinished.Wait(TimeSpan.FromSeconds(4)), Is.True);
                Assert.Throws<AltException>(() => handler.Send(new CommandParams("echo", null)));
                Assert.That(server.AcceptedCount, Is.EqualTo(2));
            }
            finally { handler.Close(); }
        }

        [Test]
        public void CancelRegistrationDoesNotWaitForCloseHandshake()
        {
            using var server = new LoopbackServer(registerReplacement: false, ignoreClose: true);
            var handler = Handler(server.Port, 10);
            try
            {
                handler.Connect();
                server.GetPeer(0).Abort();
                server.GetPeer(1);
                Assert.That(SpinWait.SpinUntil(() =>
                {
                    var socket = (AltWebSocketSharp.ClientWebSocket)typeof(DriverWebSocketClient)
                        .GetField("wsClient", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Client(handler));
                    return socket != null && socket.ReadyState == AltWebSocketSharp.WebSocketState.Open;
                }, TimeSpan.FromSeconds(2)), Is.True);
                var elapsed = Stopwatch.StartNew();
                handler.Close();
                Assert.That(elapsed.Elapsed.TotalSeconds, Is.LessThan(1), "Cancellation waited for an unregistered peer's close reply");
                Assert.Throws<AltException>(() => handler.Send(new CommandParams("echo", null)));
            }
            finally { handler.Close(); }
        }

        [Test]
        public void ExplicitFreshConnectAfterCloseWorks()
        {
            using var server = new LoopbackServer();
            var handler = Handler(server.Port);
            try
            {
                handler.Connect();
                handler.Close();
                handler.Connect();
                var command = new CommandParams("echo", null);
                handler.Send(command);
                Assert.That(handler.Recvall<string>(command), Is.EqualTo("ok"));
            }
            finally { handler.Close(); }
        }

        [Test]
        public void ConnectOnRegisteredSessionIsIdempotent()
        {
            using var server = new LoopbackServer();
            var handler = Handler(server.Port);
            try
            {
                handler.Connect();
                var client = Client(handler);
                handler.Connect();
                Assert.That(Client(handler), Is.SameAs(client));
                Assert.That(server.AcceptedCount, Is.EqualTo(1));
            }
            finally { handler.Close(); }
        }

        [Test]
        public void CloseBeforeConnectIsSafe()
        {
            var handler = Handler(1);
            Assert.DoesNotThrow(() => handler.Close());
        }

        [Test]
        public void CommandSentBeforeDisconnectCannotReceiveFromReplacementSession()
        {
            using var server = new LoopbackServer();
            var handler = Handler(server.Port);
            handler.SetCommandTimeout(1);
            using var callbackFinished = new ManualResetEventSlim();
            try
            {
                handler.Connect();
                var command = new CommandParams("echo", null);
                handler.Send(command);
                Client(handler).OnCloseEvent += (_, __) => callbackFinished.Set();
                server.GetPeer(0).Abort();
                Assert.That(callbackFinished.Wait(TimeSpan.FromSeconds(5)), Is.True);
                Assert.Throws<AltException>(() => handler.Recvall<string>(command));
            }
            finally { handler.Close(); }
        }

        [Test]
        public void ConcurrentCommandsHaveUniqueIdsAndMatchedResponses()
        {
            using var server = new LoopbackServer();
            var handler = Handler(server.Port);
            handler.SetCommandTimeout(3);
            var ids = new ConcurrentDictionary<string, bool>();
            try
            {
                handler.Connect();
                Parallel.For(0, 1000, new ParallelOptions { MaxDegreeOfParallelism = 8 }, i =>
                {
                    var command = new CommandParams("echo" + i, null);
                    handler.Send(command);
                    Assert.That(ids.TryAdd(command.messageId, true), Is.True, "duplicate message ID");
                    Assert.That(handler.Recvall<string>(command), Is.EqualTo("ok"));
                });
            }
            finally { handler.Close(); }
        }

        [Test]
        public void MultipleResponsesForOneCommandRemainOrdered()
        {
            using var server = new LoopbackServer();
            var handler = Handler(server.Port);
            try
            {
                handler.Connect();
                var command = new CommandParams("twoResponses", null);
                handler.Send(command);
                Assert.That(handler.Recvall<string>(command), Is.EqualTo("ok"));
                Assert.That(handler.Recvall<string>(command), Is.EqualTo("second"));
            }
            finally { handler.Close(); }
        }

        [Test]
        public void LateResponseForTimedOutCommandIsDiscarded()
        {
            using var server = new LoopbackServer();
            var handler = Handler(server.Port);
            handler.SetCommandTimeout(1);
            using var delivered = new ManualResetEventSlim();
            try
            {
                handler.Connect();
                var command = new CommandParams("noResponse", null);
                handler.Send(command);
                Assert.Throws<CommandResponseTimeoutException>(() => handler.Recvall<string>(command));
                Client(handler).OnMessage += (_, e) => { if (e.Data.Contains(command.messageId)) delivered.Set(); };
                server.GetPeer(0).Send(new CommandResponse { messageId = command.messageId,
                    commandName = command.commandName, data = "\"late\"" }, CancellationToken.None).GetAwaiter().GetResult();
                Assert.That(delivered.Wait(TimeSpan.FromSeconds(2)), Is.True);
                var buffered = (System.Collections.IDictionary)typeof(DriverCommunicationHandler)
                    .GetField("messages", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(handler);
                lock (buffered) Assert.That(buffered.Count, Is.Zero);
            }
            finally { handler.Close(); }
        }

        [Test]
        public void StaleRegistrationDoesNotRegisterReplacementSocket()
        {
            using var server = new LoopbackServer(registerReplacement: false);
            var handler = Handler(server.Port, 1);
            using var callbackFinished = new ManualResetEventSlim();
            try
            {
                handler.Connect();
                var oldClient = Client(handler);
                oldClient.OnCloseEvent += (_, __) => callbackFinished.Set();
                server.GetPeer(0).Abort();
                server.GetPeer(1);
                typeof(DriverCommunicationHandler).GetMethod("OnMessage", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(handler, new object[] { oldClient, JsonConvert.SerializeObject(new CommandResponse
                        { commandName = "driverRegistered", isNotification = true }) });
                Assert.That(Client(handler).DriverRegisteredCalled, Is.False);
                Assert.That(callbackFinished.Wait(TimeSpan.FromSeconds(3)), Is.True);
                Assert.Throws<AltException>(() => handler.Send(new CommandParams("echo", null)));
            }
            finally { handler.Close(); }
        }

        private sealed class LoopbackServer : IDisposable
        {
            private readonly TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            private readonly CancellationTokenSource stop = new CancellationTokenSource();
            private readonly ConcurrentQueue<Peer> peers = new ConcurrentQueue<Peer>();
            private readonly ConcurrentBag<Task> sessions = new ConcurrentBag<Task>();
            private readonly Task accepting;
            private readonly bool register;
            private readonly bool registerReplacement;
            private readonly bool abortUnregistered;
            private readonly bool ignoreClose;
            internal int AcceptedCount => peers.Count;
            internal int Port => ((IPEndPoint)listener.LocalEndpoint).Port;

            internal LoopbackServer(bool register = true, bool registerReplacement = true, bool abortUnregistered = false, bool ignoreClose = false)
            {
                this.register = register;
                this.registerReplacement = registerReplacement;
                this.abortUnregistered = abortUnregistered;
                this.ignoreClose = ignoreClose;
                listener.Start();
                accepting = Task.Run(Accept);
            }

            internal Peer GetPeer(int index)
            {
                Assert.That(SpinWait.SpinUntil(() => peers.Count > index, TimeSpan.FromSeconds(5)), Is.True);
                return peers.ToArray()[index];
            }

            private async Task Accept()
            {
                try
                {
                    while (!stop.IsCancellationRequested)
                    {
                        var peer = new Peer(await listener.AcceptTcpClientAsync());
                        peers.Enqueue(peer);
                        var index = peers.Count - 1;
                        sessions.Add(Task.Run(() => Serve(peer, index)));
                    }
                }
                catch (Exception) when (stop.IsCancellationRequested) { }
            }

            private async Task Serve(Peer peer, int index)
            {
                try
                {
                    await peer.Upgrade(stop.Token);
                    if (abortUnregistered && (!register || (index > 0 && !registerReplacement)))
                    {
                        peer.Abort();
                        return;
                    }
                    if (register && (index == 0 || registerReplacement))
                        await peer.Send(new CommandResponse { commandName = "driverRegistered", isNotification = true }, stop.Token);
                    var bytes = new byte[16384];
                    while (!stop.IsCancellationRequested)
                    {
                        var message = await peer.Socket.ReceiveAsync(new ArraySegment<byte>(bytes), stop.Token);
                        if (message.MessageType == WebSocketMessageType.Close)
                        {
                            if (ignoreClose) await Task.Delay(Timeout.Infinite, stop.Token);
                            await peer.Socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "", stop.Token);
                            return;
                        }
                        var command = JsonConvert.DeserializeObject<CommandParams>(Encoding.UTF8.GetString(bytes, 0, message.Count));
                        if (command.commandName == "noResponse") continue;
                        await peer.Send(new CommandResponse { messageId = command.messageId,
                            commandName = command.commandName, data = "\"ok\"" }, stop.Token);
                        if (command.commandName == "twoResponses")
                            await peer.Send(new CommandResponse { messageId = command.messageId,
                                commandName = command.commandName, data = "\"second\"" }, stop.Token);
                    }
                }
                catch (Exception) when (stop.IsCancellationRequested || peer.Aborted) { }
                catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely) { }
            }

            public void Dispose()
            {
                stop.Cancel();
                listener.Stop();
                foreach (var peer in peers) peer.Abort();
                Assert.That(accepting.Wait(TimeSpan.FromSeconds(5)), Is.True);
                Assert.That(Task.WaitAll(sessions.ToArray(), TimeSpan.FromSeconds(5)), Is.True);
                stop.Dispose();
            }
        }

        private sealed class Peer
        {
            private readonly TcpClient tcp;
            internal WebSocket Socket;
            internal volatile bool Aborted;
            internal Peer(TcpClient tcp) { this.tcp = tcp; }

            internal async Task Upgrade(CancellationToken token)
            {
                var stream = tcp.GetStream();
                var header = new StringBuilder();
                var one = new byte[1];
                while (!header.ToString().EndsWith("\r\n\r\n", StringComparison.Ordinal))
                {
                    if (await stream.ReadAsync(one, 0, 1, token) == 0) throw new EndOfStreamException();
                    header.Append((char)one[0]);
                    if (header.Length > 16384) throw new InvalidDataException("Oversized handshake");
                }
                string key = null;
                foreach (var line in header.ToString().Split('\n'))
                    if (line.StartsWith("Sec-WebSocket-Key:", StringComparison.OrdinalIgnoreCase))
                        key = line.Substring(line.IndexOf(':') + 1).Trim();
                using var sha = SHA1.Create();
                var accept = Convert.ToBase64String(sha.ComputeHash(Encoding.ASCII.GetBytes(key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));
                var response = Encoding.ASCII.GetBytes("HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: " + accept + "\r\n\r\n");
                await stream.WriteAsync(response, 0, response.Length, token);
                Socket = WebSocket.CreateFromStream(stream, true, null, Timeout.InfiniteTimeSpan);
            }

            internal Task Send(CommandResponse response, CancellationToken token) => Socket.SendAsync(
                new ArraySegment<byte>(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(response))),
                WebSocketMessageType.Text, true, token);

            internal void Abort()
            {
                Aborted = true;
                Socket?.Abort();
                tcp.Dispose();
            }
        }
    }
}
