namespace DmCompanion.Desktop;

public static class LoopbackGuard
{
    // Falla cerrado: una dirección que no se pueda interpretar (http://*:5190, http://+:5190) cuenta como no loopback.
    // Un conjunto vacío (feature ausente) tampoco es loopback: no se puede demostrar, así que se trata como expuesto.
    public static bool IsLoopbackOnly(IEnumerable<string> addresses)
    {
        var list = addresses.ToList();
        return list.Count > 0 && list.All(address => Uri.TryCreate(address, UriKind.Absolute, out var uri) && uri.IsLoopback);
    }
}
