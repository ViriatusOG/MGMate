using System.Net;
using System.Net.Sockets;

namespace MGMate.Worker.Services;

/// <summary>
/// Connections for push deliveries, which go to whatever endpoint a browser handed in. The API
/// already refuses endpoints that are not a public https name, but a public name can still point
/// at a private address, or be re-pointed there after it was checked. So the address is checked
/// again here, at the moment of connecting, and only public ones are dialled.
/// </summary>
internal static class PublicAddressConnection
{
    private static readonly System.Net.IPNetwork[] NonPublicNetworks =
    [
        System.Net.IPNetwork.Parse("0.0.0.0/8"),
        System.Net.IPNetwork.Parse("10.0.0.0/8"),
        System.Net.IPNetwork.Parse("100.64.0.0/10"),
        System.Net.IPNetwork.Parse("127.0.0.0/8"),
        System.Net.IPNetwork.Parse("169.254.0.0/16"),
        System.Net.IPNetwork.Parse("172.16.0.0/12"),
        System.Net.IPNetwork.Parse("192.0.0.0/24"),
        System.Net.IPNetwork.Parse("192.168.0.0/16"),
        System.Net.IPNetwork.Parse("198.18.0.0/15"),
        System.Net.IPNetwork.Parse("224.0.0.0/3"),
        System.Net.IPNetwork.Parse("::/128"),
        System.Net.IPNetwork.Parse("::1/128"),
        System.Net.IPNetwork.Parse("64:ff9b:1::/48"),
        System.Net.IPNetwork.Parse("fc00::/7"),
        System.Net.IPNetwork.Parse("fe80::/10"),
        System.Net.IPNetwork.Parse("fec0::/10"),
        System.Net.IPNetwork.Parse("ff00::/8"),
    ];

    public static SocketsHttpHandler CreateHandler() => new()
    {
        ConnectCallback = ConnectAsync,
        // Re-resolve now and then rather than keeping one connection, and with it one address,
        // for the life of the process.
        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
    };

    private static async ValueTask<Stream> ConnectAsync(SocketsHttpConnectionContext context, CancellationToken ct)
    {
        var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, ct);
        var allowed = addresses.Where(IsPublic).ToArray();
        if (allowed.Length == 0)
            throw new HttpRequestException("The push endpoint's host resolves to no public address.");

        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(allowed, context.DnsEndPoint.Port, ct);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    internal static bool IsPublic(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        return !NonPublicNetworks.Any(network => network.Contains(address));
    }
}
