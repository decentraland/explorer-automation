using System.Net.Sockets;
using ExplorerAutomation.AltRelay;

namespace ExplorerAutomation.Tests.Common;

/// <summary>
/// Runs the in-process AltTester relay when nothing already listens on the AltTester port, so the
/// suite needs no AltTester Desktop (and no license). A running Desktop is used as before.
/// </summary>
public static class EmbeddedRelay
{
    public const string HOST = RelayServer.DEFAULT_HOST;
    public const int PORT = RelayServer.DEFAULT_PORT;
    private const string DISABLE_ENV = "ALT_RELAY";

    private static RelayServer _server;

    public static void EnsureStarted()
    {
        if (string.Equals(Environment.GetEnvironmentVariable(DISABLE_ENV), "0", StringComparison.Ordinal))
        {
            Reporter.Log($"{DISABLE_ENV}=0: embedded relay disabled, expecting AltTester Desktop on {HOST}:{PORT}");
            return;
        }
        if (RelayServer.IsPortOpen(HOST, PORT))
        {
            Reporter.Log($"AltTester server already listening on {HOST}:{PORT}; embedded relay not started");
            return;
        }
        try
        {
            _server = RelayServer.Start(HOST, PORT, line => Reporter.Log($"[alt-relay] {line}"));
            Reporter.Log($"Nothing on {HOST}:{PORT}; embedded relay started ({DISABLE_ENV}=0 requires AltTester Desktop instead)");
        }
        catch (SocketException ex)
        {
            Reporter.Log($"Embedded relay could not bind {HOST}:{PORT} ({ex.Message}); using whatever listens there");
        }
    }

    public static void Stop()
    {
        _server?.Dispose();
        _server = null;
    }
}
