using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace DmCompanion.Infrastructure.Tv;

/// <summary>IPv4 locales por las que una TV de la misma red puede llegar al servidor.</summary>
internal static class TvAddresses
{
    /// <summary>Interfaces activas que no son loopback; primero las de la wifi (<c>wlan*</c>, «Wi-Fi»).</summary>
    public static IReadOnlyList<string> Find()
    {
        NetworkInterface[] interfaces;
        try
        {
            interfaces = NetworkInterface.GetAllNetworkInterfaces();
        }
        catch (NetworkInformationException)
        {
            return [];
        }
        return Order(interfaces
            .Where(nic => nic.OperationalStatus == OperationalStatus.Up && nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .SelectMany(nic => nic.GetIPProperties().UnicastAddresses.Select(unicast => (nic.Name, unicast.Address))));
    }

    internal static IReadOnlyList<string> Order(IEnumerable<(string InterfaceName, IPAddress Address)> candidates) =>
        candidates
            .Where(c => c.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(c.Address) && !IsLinkLocal(c.Address))
            .OrderBy(c => IsWifi(c.InterfaceName) ? 0 : 1)
            .Select(c => c.Address.ToString())
            .Distinct()
            .ToList();

    private static bool IsWifi(string name) =>
        name.StartsWith("wlan", StringComparison.OrdinalIgnoreCase)
        || name.StartsWith("wi-fi", StringComparison.OrdinalIgnoreCase)
        || name.StartsWith("wifi", StringComparison.OrdinalIgnoreCase);

    // 169.254.x.x: dirección automática sin red real; la TV no llegaría.
    private static bool IsLinkLocal(IPAddress address) => address.GetAddressBytes() is [169, 254, ..];
}
