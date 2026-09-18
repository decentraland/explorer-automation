using System.Net.Sockets;
using ExplorerAutomation.AltRelay;

if (args.Contains("--help") || args.Contains("-h"))
{
    Console.WriteLine($"usage: AltRelay [host] [port]   (defaults {RelayServer.DEFAULT_HOST} {RelayServer.DEFAULT_PORT})");
    return 0;
}

var host = args.Length > 0 ? args[0] : RelayServer.DEFAULT_HOST;
var port = args.Length > 1 ? int.Parse(args[1]) : RelayServer.DEFAULT_PORT;

RelayServer relay;
try
{
    relay = RelayServer.Start(host, port, line => Console.WriteLine($"[alt-relay] {line}"));
}
catch (SocketException e)
{
    Console.Error.WriteLine($"AltRelay: cannot listen on {host}:{port} ({e.Message}). AltTester Desktop or another relay already there?");
    return 1;
}

using (relay)
{
    Console.WriteLine($"AltRelay: launch the Explorer with --alttester and point AltDriver at {relay.Host}:{relay.Port}. Ctrl+C stops.");
    var stop = new ManualResetEventSlim();
    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true;
        stop.Set();
    };
    stop.Wait();
}
return 0;
