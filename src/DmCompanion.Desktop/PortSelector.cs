using System.Net;
using System.Net.Sockets;

namespace DmCompanion.Desktop;

public static class PortSelector
{
    /// <summary>El puerto preferido si está libre en loopback (IPv4 e IPv6); si no, uno que asigne el sistema.</summary>
    public static int Choose(int preferred = 5190) => TryBind(preferred) ?? TryBind(0) ?? throw new IOException("No hay ningún puerto libre en loopback.");

    // ListenLocalhost enlaza 127.0.0.1 y [::1]: el puerto debe estar libre en ambos. Hay una carrera inevitable entre
    // cerrar los listeners y que Kestrel abra el puerto; es aceptable en un equipo de un solo usuario.
    private static int? TryBind(int port)
    {
        // Con puerto 0 el puerto libre en IPv4 puede estar ocupado en IPv6: se reintenta con otro.
        for (var attempt = 0; attempt < (port == 0 ? 10 : 1); attempt++)
        {
            if (TryBindOne(IPAddress.Loopback, port) is not { } v4) return null;
            if (IsFreeOnIPv6(v4)) return v4;
        }
        return null;
    }

    // Sin IPv6 en el equipo no hay nada que comprobar: no cuenta como ocupado.
    private static bool IsFreeOnIPv6(int port)
    {
        if (!Socket.OSSupportsIPv6) return true;
        try
        {
            var listener = new TcpListener(IPAddress.IPv6Loopback, port);
            listener.Start();
            listener.Stop();
            return true;
        }
        catch (SocketException ex)
        {
            return ex.SocketErrorCode is not (SocketError.AddressAlreadyInUse or SocketError.AccessDenied);
        }
    }

    private static int? TryBindOne(IPAddress address, int port)
    {
        try
        {
            var listener = new TcpListener(address, port);
            listener.Start();
            try
            {
                return ((IPEndPoint)listener.LocalEndpoint).Port;
            }
            finally
            {
                listener.Stop();
            }
        }
        catch (SocketException)
        {
            return null;
        }
    }
}
