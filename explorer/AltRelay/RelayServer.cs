using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;

namespace ExplorerAutomation.AltRelay;

/// <summary>
/// Stand-in for the relay inside AltTester Desktop: pairs one instrumented app (ws path /altws/app)
/// with one AltDriver (ws path /altws) and forwards their frames verbatim. Needs no license.
/// </summary>
public sealed class RelayServer : IDisposable
{
    public const string DEFAULT_HOST = "127.0.0.1";
    public const int DEFAULT_PORT = 13000;

    private const string WS_GUID = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
    private const string APP_PATH = "/altws/app";
    private const string DRIVER_PATH = "/altws";
    private const int APP_DISCONNECTED_CLOSE_CODE = 4002;
    private const int MAX_HANDSHAKE_BYTES = 16 * 1024;
    private const int MAX_PENDING_MESSAGES = 256;
    private const string DRIVER_REGISTERED =
        "{\"commandName\":\"driverRegistered\",\"isNotification\":true,\"messageId\":\"\",\"driverId\":\"\",\"data\":null,\"error\":null}";
    private const string DRIVER_CONNECTED = "DriverConnectedNotification";
    private const string DRIVER_DISCONNECTED = "DriverDisconnectedNotification";

    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _cts = new();
    private readonly CancellationToken _ct;
    private readonly object _lock = new();
    private readonly List<Peer> _apps = [];
    private readonly List<Peer> _drivers = [];
    private readonly Action<string> _log;
    private Task? _acceptLoop;

    public string Host { get; }
    public int Port { get; private set; }

    private RelayServer(string host, int port, Action<string>? log)
    {
        Host = host;
        Port = port;
        _log = log ?? (_ => { });
        _ct = _cts.Token;
        _listener = new TcpListener(IPAddress.Parse(host), port);
    }

    /// <summary>Binds the port and starts accepting. Throws <see cref="SocketException"/> when the port is taken.</summary>
    public static RelayServer Start(string host = DEFAULT_HOST, int port = DEFAULT_PORT, Action<string>? log = null)
    {
        var server = new RelayServer(host, port, log);
        server._listener.Start();
        server.Port = ((IPEndPoint)server._listener.LocalEndpoint).Port;
        server._acceptLoop = Task.Run(server.AcceptLoopAsync);
        server._log($"listening on ws://{server.Host}:{server.Port} (app: {APP_PATH}, driver: {DRIVER_PATH})");
        return server;
    }

    /// <summary>True when something (AltTester Desktop, another relay) already accepts connections there.</summary>
    public static bool IsPortOpen(string host, int port, int timeoutMs = 500)
    {
        try
        {
            using var client = new TcpClient();
            return client.ConnectAsync(host, port).Wait(timeoutMs) && client.Connected;
        }
        catch (Exception e) when (e is AggregateException or SocketException)
        {
            return false;
        }
    }

    public void Dispose()
    {
        if (_cts.IsCancellationRequested) return;
        _cts.Cancel();
        _listener.Stop();
        List<Peer> peers;
        lock (_lock) peers = [.. _apps, .. _drivers];
        foreach (var peer in peers) peer.Socket.Abort();
        try { _acceptLoop?.Wait(TimeSpan.FromSeconds(2)); }
        catch (AggregateException) { }
        _cts.Dispose();
    }

    private async Task AcceptLoopAsync()
    {
        while (!_ct.IsCancellationRequested)
        {
            TcpClient client;
            try { client = await _listener.AcceptTcpClientAsync(_ct); }
            catch (Exception e) when (e is OperationCanceledException or ObjectDisposedException or SocketException)
            {
                if (_ct.IsCancellationRequested) return;
                _log($"accept failed: {e.Message}");
                continue;
            }
            _ = ServeAsync(client);
        }
    }

    private async Task ServeAsync(TcpClient client)
    {
        var remote = client.Client.RemoteEndPoint?.ToString() ?? "?";
        try
        {
            using (client)
            {
                client.NoDelay = true;
                var stream = client.GetStream();
                var request = await ReadHandshakeAsync(stream, _ct);
                if (request is null) return;
                var (path, query, key) = request.Value;
                var role = path switch
                {
                    APP_PATH => Role.App,
                    DRIVER_PATH => Role.Driver,
                    _ => (Role?)null,
                };
                if (role is null || key is null)
                {
                    _log($"rejected {remote}: GET {path} is not an AltTester endpoint");
                    await WriteAsync(stream, "HTTP/1.1 404 Not Found\r\nContent-Length: 0\r\nConnection: close\r\n\r\n", _ct);
                    return;
                }
                var accept = Convert.ToBase64String(SHA1.HashData(Encoding.ASCII.GetBytes(key + WS_GUID)));
                await WriteAsync(stream, "HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: " + accept + "\r\n\r\n", _ct);
                using var socket = WebSocket.CreateFromStream(stream, new WebSocketCreationOptions { IsServer = true, KeepAliveInterval = TimeSpan.Zero });
                var peer = new Peer(socket, role.Value, AppNameOf(query));
                _log($"{peer} connected from {remote}");
                Register(peer);
                try { await PumpAsync(peer); }
                finally
                {
                    Unregister(peer);
                    _log($"{peer} disconnected");
                }
            }
        }
        catch (Exception e) when (e is IOException or SocketException or WebSocketException or OperationCanceledException or ObjectDisposedException)
        {
            _log($"{remote} dropped: {e.Message}");
        }
    }

    private static async Task<(string Path, string Query, string? Key)?> ReadHandshakeAsync(Stream stream, CancellationToken ct)
    {
        // One byte at a time so nothing past the headers is consumed before the WebSocket takes over the stream.
        var bytes = new List<byte>();
        var one = new byte[1];
        while (bytes.Count < MAX_HANDSHAKE_BYTES)
        {
            if (await stream.ReadAsync(one, ct) == 0) return null;
            bytes.Add(one[0]);
            var n = bytes.Count;
            if (n >= 4 && bytes[n - 4] == '\r' && bytes[n - 3] == '\n' && bytes[n - 2] == '\r' && bytes[n - 1] == '\n') break;
        }
        var lines = Encoding.ASCII.GetString(bytes.ToArray()).Split("\r\n");
        var requestLine = lines[0].Split(' ');
        if (requestLine.Length < 2 || requestLine[0] != "GET") return null;
        var target = requestLine[1];
        var q = target.IndexOf('?');
        var path = q < 0 ? target : target[..q];
        var query = q < 0 ? "" : target[(q + 1)..];
        string? key = null;
        foreach (var line in lines.Skip(1))
        {
            var colon = line.IndexOf(':');
            if (colon > 0 && line[..colon].Trim().Equals("Sec-WebSocket-Key", StringComparison.OrdinalIgnoreCase))
                key = line[(colon + 1)..].Trim();
        }
        return (path, query, key);
    }

    private static string AppNameOf(string query)
    {
        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = pair.IndexOf('=');
            if (eq > 0 && pair[..eq] == "appName") return Uri.UnescapeDataString(pair[(eq + 1)..]);
        }
        return "";
    }

    private static Task WriteAsync(Stream stream, string text, CancellationToken ct) =>
        stream.WriteAsync(Encoding.ASCII.GetBytes(text), ct).AsTask();

    private async Task PumpAsync(Peer peer)
    {
        var buffer = new byte[64 * 1024];
        using var message = new MemoryStream();
        while (!_ct.IsCancellationRequested)
        {
            var result = await peer.Socket.ReceiveAsync(buffer, _ct);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                await peer.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye");
                return;
            }
            message.Write(buffer, 0, result.Count);
            if (!result.EndOfMessage) continue;
            var text = Encoding.UTF8.GetString(message.GetBuffer(), 0, (int)message.Length);
            message.SetLength(0);
            await DeliverAsync(peer, text);
        }
    }

    private Task DeliverAsync(Peer from, string text)
    {
        Peer? to;
        lock (_lock)
        {
            to = from.Mate;
            if (to is null)
            {
                if (from.Pending.Count < MAX_PENDING_MESSAGES) from.Pending.Add(text);
                return Task.CompletedTask;
            }
        }
        return to.SendAsync(text);
    }

    private void Register(Peer peer)
    {
        lock (_lock)
        {
            (peer.Role == Role.App ? _apps : _drivers).Add(peer);
            TryPairLocked();
        }
    }

    private void Unregister(Peer peer)
    {
        Peer? mate;
        lock (_lock)
        {
            _apps.Remove(peer);
            _drivers.Remove(peer);
            mate = peer.Mate;
            peer.Mate = null;
            if (mate is not null) mate.Mate = null;
            if (mate is not null && peer.Role == Role.Driver)
            {
                // Started before re-pairing so the app sees this driver leave before the next one arrives.
                _ = mate.SendAsync(DriverNotification(DRIVER_DISCONNECTED, peer));
                TryPairLocked();
            }
        }
        if (mate is not null && peer.Role == Role.App)
        {
            _log($"{mate} loses its app; closing it with {APP_DISCONNECTED_CLOSE_CODE}");
            _ = mate.CloseAsync((WebSocketCloseStatus)APP_DISCONNECTED_CLOSE_CODE, "App disconnected");
        }
    }

    private void TryPairLocked()
    {
        foreach (var driver in _drivers)
        {
            if (driver.Mate is not null) continue;
            var app = _apps.FirstOrDefault(a => a.Mate is null && a.AppName.Length > 0 && a.AppName == driver.AppName)
                      ?? _apps.FirstOrDefault(a => a.Mate is null);
            if (app is null) continue;
            driver.Mate = app;
            app.Mate = driver;
            var backlog = app.Pending.ToArray();
            app.Pending.Clear();
            _log($"paired {driver} with {app}");
            _ = Task.Run(async () =>
            {
                // The app only switches to its simulated input devices once it is told a driver is there.
                await app.SendAsync(DriverNotification(DRIVER_CONNECTED, driver));
                foreach (var text in backlog) await driver.SendAsync(text);
                await driver.SendAsync(DRIVER_REGISTERED);
            });
        }
    }

    private static string DriverNotification(string name, Peer driver) =>
        $"{{\"commandName\":\"{name}\",\"isNotification\":true,\"messageId\":\"\",\"driverId\":\"{driver.Id}\"}}";

    private enum Role { App, Driver }

    private sealed class Peer(WebSocket socket, Role role, string appName)
    {
        private readonly SemaphoreSlim _sendGate = new(1, 1);

        public WebSocket Socket { get; } = socket;
        public string Id { get; } = Guid.NewGuid().ToString("N");
        public Role Role { get; } = role;
        public string AppName { get; } = appName;
        public Peer? Mate { get; set; }
        public List<string> Pending { get; } = [];

        public async Task SendAsync(string text)
        {
            await _sendGate.WaitAsync();
            try
            {
                if (Socket.State != WebSocketState.Open) return;
                await Socket.SendAsync(Encoding.UTF8.GetBytes(text), WebSocketMessageType.Text, true, CancellationToken.None);
            }
            catch (Exception e) when (e is WebSocketException or IOException or ObjectDisposedException or InvalidOperationException) { }
            finally { _sendGate.Release(); }
        }

        public async Task CloseAsync(WebSocketCloseStatus status, string reason)
        {
            await _sendGate.WaitAsync();
            try
            {
                if (Socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
                    await Socket.CloseOutputAsync(status, reason, CancellationToken.None);
            }
            catch (Exception e) when (e is WebSocketException or IOException or ObjectDisposedException or InvalidOperationException) { }
            finally { _sendGate.Release(); }
        }

        public override string ToString() => $"{Role.ToString().ToLowerInvariant()} '{AppName}'";
    }
}
