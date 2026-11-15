using System.Net;
using DmCompanion.Infrastructure.Tv;

namespace DmCompanion.Infrastructure.Tests.Tv;

public sealed class TvAddressesTests
{
    [Fact]
    public void Wifi_addresses_come_first_and_loopback_ipv6_and_link_local_are_skipped()
    {
        var addresses = TvAddresses.Order(
        [
            ("eth0", IPAddress.Parse("10.0.0.5")),
            ("lo", IPAddress.Loopback),
            ("wlan0", IPAddress.Parse("fe80::1")),
            ("eth1", IPAddress.Parse("169.254.3.4")),
            ("wlan0", IPAddress.Parse("192.168.1.20")),
        ]);

        addresses.Should().Equal("192.168.1.20", "10.0.0.5");
    }

    [Fact]
    public void Windows_wifi_adapter_counts_as_wifi()
    {
        var addresses = TvAddresses.Order([("Ethernet", IPAddress.Parse("10.0.0.5")), ("Wi-Fi", IPAddress.Parse("192.168.1.20"))]);

        addresses[0].Should().Be("192.168.1.20");
    }
}
