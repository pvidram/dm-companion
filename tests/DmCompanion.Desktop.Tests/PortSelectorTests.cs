using System.Net;
using System.Net.Sockets;

namespace DmCompanion.Desktop.Tests;

public sealed class PortSelectorTests
{
    [Fact]
    public void Choose_returns_the_preferred_port_when_it_is_free()
    {
        var free = FreePort();

        PortSelector.Choose(free).Should().Be(free);
    }

    [Fact]
    public void Choose_returns_another_port_when_the_preferred_one_is_taken()
    {
        var taken = new TcpListener(IPAddress.Loopback, 0);
        taken.Start();
        try
        {
            var busy = ((IPEndPoint)taken.LocalEndpoint).Port;

            var chosen = PortSelector.Choose(busy);

            chosen.Should().BeGreaterThan(0).And.NotBe(busy);
        }
        finally
        {
            taken.Stop();
        }
    }

    [Fact]
    public void Choose_returns_another_port_when_the_preferred_one_is_taken_only_on_IPv6()
    {
        // Sin IPv6 en el equipo no hay nada que ocupar: el test no aplica.
        if (!Socket.OSSupportsIPv6) return;
        var taken = new TcpListener(IPAddress.IPv6Loopback, 0);
        taken.Start();
        try
        {
            var busy = ((IPEndPoint)taken.LocalEndpoint).Port;

            PortSelector.Choose(busy).Should().NotBe(busy);
        }
        finally
        {
            taken.Stop();
        }
    }

    private static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
