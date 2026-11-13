using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace DmCompanion.DevHost;

public static class KestrelDetection
{
    // El IServer real es KestrelServerImpl (interno), que no hereda de la clase pública KestrelServer:
    // un `is KestrelServer` da siempre false. Se compara el ensamblado, y TestServer (otro ensamblado) queda fuera.
    public static bool IsKestrel(IServer server) => server.GetType().Assembly == typeof(KestrelServer).Assembly;
}
