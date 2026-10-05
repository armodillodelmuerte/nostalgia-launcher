using System.Diagnostics;
using System.Net.Sockets;

namespace Nostalgia.Launcher.Core.Status;

public sealed record ServerStatus(bool Online, int? LatencyMs, string? Error, DateTimeOffset CheckedAt);

/// <summary>Online check = TCP connect to the login port (no login, nothing sent). Latency = connect time.</summary>
public static class ServerStatusProbe
{
    public static async Task<ServerStatus> ProbeAsync(string host, int port, TimeSpan timeout, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(host))
            return new(false, null, "no host", DateTimeOffset.Now);
        using var client = new TcpClient();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);
        var sw = Stopwatch.StartNew();
        try
        {
            await client.ConnectAsync(host, port, cts.Token);
            sw.Stop();
            return new(true, (int)Math.Max(1, sw.ElapsedMilliseconds), null, DateTimeOffset.Now);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return new(false, null, "timeout", DateTimeOffset.Now);
        }
        catch (SocketException e)
        {
            return new(false, null, e.SocketErrorCode.ToString(), DateTimeOffset.Now);
        }
    }
}
