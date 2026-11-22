using System.Net;
using System.Net.Sockets;

namespace Nights.Infrastructure.Security;

/// <summary>
/// Tailscale CGNAT ve yerel ağ IP kontrolleri.
/// </summary>
public static class IpAddressExtensions
{
    /// <summary>
    /// IP adresinin Tailscale CGNAT ağına (100.64.0.0/10) veya Localhost'a ait olup olmadığını doğrular.
    /// </summary>
    public static bool IsTailscaleOrLocalhost(this IPAddress? ip)
    {
        if (ip == null) return false;

        // 1. IPv6 veya IPv4 Loopback (127.0.0.1, ::1)
        if (IPAddress.IsLoopback(ip)) return true;

        // 2. IPv6 ile haritalanmış IPv4 adresi kontrolü (::ffff:100.x.x.x)
        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (ip.IsIPv4MappedToIPv6)
            {
                ip = ip.MapToIPv4();
            }
            else
            {
                return false;
            }
        }

        var bytes = ip.GetAddressBytes();
        if (bytes.Length != 4) return false;

        // 3. Tailscale CGNAT (100.64.0.0/10): İlk bayt 100, ikinci bayt 64-127 arası
        return bytes[0] == 100 && (bytes[1] >= 64 && bytes[1] <= 127);
    }
}
