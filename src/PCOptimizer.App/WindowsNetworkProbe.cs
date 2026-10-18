using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using PCOptimizer.Core;

namespace PCOptimizer.App;

internal sealed record AdapterObservation(string Name, string Type, long? LinkBitsPerSecond, NetworkCounterRate Rate,
    long? InputErrors, long? OutputErrors, string DnsServers, string Gateways, string? Error);
internal sealed record DnsObservation(string Host, double? ElapsedMs, int AddressCount, string? Error);
internal static class WindowsNetworkProbe
{
    public static async Task<AdapterObservation[]> AdaptersAsync(CancellationToken token)
    {
        var devices = NetworkInterface.GetAllNetworkInterfaces().Where(n => n.OperationalStatus == OperationalStatus.Up
            && n.NetworkInterfaceType != NetworkInterfaceType.Loopback).ToArray();
        var before = new Dictionary<string, (long Received, long Sent)>();
        foreach (var n in devices)
        {
            try { var s = n.GetIPStatistics(); before[n.Id] = (s.BytesReceived, s.BytesSent); }
            catch (Exception ex) when (ex is NetworkInformationException or NotSupportedException) { }
        }
        var timer = Stopwatch.StartNew(); await Task.Delay(2000, token);
        var observations = new List<AdapterObservation>();
        foreach (var n in devices)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                var s = n.GetIPStatistics(); var elapsed = timer.Elapsed.TotalSeconds; var info = n.GetIPProperties();
                var rate = before.TryGetValue(n.Id, out var b) ? NetworkCounterRate.Calculate(b.Received, s.BytesReceived, b.Sent, s.BytesSent, elapsed)
                    : new NetworkCounterRate(null, null, "No se obtuvo un contador inicial.");
                observations.Add(new(n.Name, n.NetworkInterfaceType.ToString(), n.Speed > 0 ? n.Speed : null, rate,
                    s.IncomingPacketsWithErrors, s.OutgoingPacketsWithErrors, string.Join(", ", info.DnsAddresses),
                    string.Join(", ", info.GatewayAddresses.Select(g => g.Address)), null));
            }
            catch (Exception ex) when (ex is NetworkInformationException or NotSupportedException) { observations.Add(new(n.Name, n.NetworkInterfaceType.ToString(), null, new(null, null, "Sin medición"), null, null, "", "", ex.Message)); }
        }
        return observations.ToArray();
    }
    public static async Task<DnsObservation[]> DnsAsync(CancellationToken token)
    {
        // Fixed hostnames; an explicit UI action performs resolution, never downloads or sends load traffic.
        return await Task.WhenAll(new[] { "www.microsoft.com", "www.cloudflare.com" }.Select(async host =>
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromSeconds(5));
            var watch = Stopwatch.StartNew();
            try
            {
                var addresses = await Dns.GetHostAddressesAsync(host, timeout.Token);
                return new DnsObservation(host, watch.Elapsed.TotalMilliseconds, addresses.Length, null);
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested) { return new DnsObservation(host, null, 0, "Timeout de cinco segundos."); }
            catch (SocketException ex) { return new DnsObservation(host, null, 0, ex.Message); }
        }));
    }
}
