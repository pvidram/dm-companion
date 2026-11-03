namespace DmCompanion.DevHost;

public static class DevHostBinding
{
    public const int Port = 5180;

    // Falla cerrado: cualquier dirección que no se pueda interpretar (http://*:5180, http://+:5180) cuenta como no loopback.
    // Un conjunto vacío (feature ausente) tampoco es loopback: no se puede demostrar, así que se trata como expuesto.
    public static bool IsLoopbackOnly(IEnumerable<string> addresses)
    {
        var list = addresses.ToList();
        return list.Count > 0 && list.All(address => Uri.TryCreate(address, UriKind.Absolute, out var uri) && uri.IsLoopback);
    }
}
