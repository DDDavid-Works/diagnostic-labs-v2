using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace DiagnosticLabs.StationSetup.Core;

internal static class Network
{
    public static async Task<IPAddress[]> ResolveAsync(string host, CancellationToken ct = default)
    {
        if (IPAddress.TryParse(host, out var ip)) return [ip];
        try
        {
            return (await Dns.GetHostAddressesAsync(host, ct)).Where(a => a.AddressFamily == AddressFamily.InterNetwork).ToArray();
        }
        catch (SocketException)
        {
            return [];
        }
    }

    public static async Task<bool> CanConnectAsync(string host, int port, TimeSpan timeout, CancellationToken ct = default)
    {
        using var client = new TcpClient();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);
        try
        {
            await client.ConnectAsync(host, port, cts.Token);
            return true;
        }
        catch (Exception ex) when (ex is SocketException or OperationCanceledException && !ct.IsCancellationRequested)
        {
            return false;
        }
    }

    /// <summary>
    /// This PC's IPv4 addresses for the server tab. Cards with a gateway first (the lab network); virtual
    /// adapters (Hyper-V, VirtualBox) have none, so they only show when nothing else does.
    /// </summary>
    public static IReadOnlyList<string> LocalIPv4()
    {
        var cards = NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .Select(n => n.GetIPProperties())
            .ToList();
        var routed = cards.Where(p => p.GatewayAddresses.Any(g => g.Address.AddressFamily == AddressFamily.InterNetwork && !g.Address.Equals(IPAddress.Any))).ToList();
        return Addresses(routed.Count > 0 ? routed : cards);
    }

    private static List<string> Addresses(IEnumerable<IPInterfaceProperties> cards) =>
        cards.SelectMany(p => p.UnicastAddresses)
            .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a.Address))
            .Select(a => a.Address.ToString())
            .Where(a => !a.StartsWith("169.254.", StringComparison.Ordinal))
            .Distinct()
            .ToList();
}
