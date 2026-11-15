using System.Security.Cryptography;
using System.Text;

namespace DmCompanion.Infrastructure.Tv;

internal enum TvRouteKind { NotFound, RedirectToIndex, Index, Asset, Events, Size, File }

/// <summary>
/// Rutas de la TV, todas bajo <c>/tv/{código}/</c>. La ruta llega sin decodificar: un nombre con «%», «..» o «/» de más
/// no coincide con nada (los nombres de recursos e ids de ficheros se buscan tal cual en listas cerradas).
/// </summary>
internal readonly record struct TvRoute(TvRouteKind Kind, string Name = "")
{
    private static readonly TvRoute NotFound = new(TvRouteKind.NotFound);

    public static TvRoute Match(string path, string code)
    {
        var segments = path.Split('/');
        // "/tv/{código}" → ["", "tv", código]; "/tv/{código}/" → ["", "tv", código, ""].
        if (segments.Length < 3 || segments[0].Length != 0 || segments[1] != "tv" || !SameCode(segments[2], code)) return NotFound;
        var rest = segments[3..];
        return rest switch
        {
            [] => new TvRoute(TvRouteKind.RedirectToIndex),
            [""] => new TvRoute(TvRouteKind.Index),
            ["events"] => new TvRoute(TvRouteKind.Events),
            ["size"] => new TvRoute(TvRouteKind.Size),
            ["assets", var name] when IsPlainName(name) => new TvRoute(TvRouteKind.Asset, name),
            ["files", var id] when IsPlainName(id) => new TvRoute(TvRouteKind.File, id),
            _ => NotFound,
        };
    }

    private static bool IsPlainName(string name) =>
        name.Length > 0 && name != "." && name != ".." && !name.Contains('%') && !name.Contains('\\');

    // Sin distinguir mayúsculas y en tiempo constante: el código es lo único que protege el servidor. Solo se pasan a minúscula
    // las letras ASCII (el código nunca lleva otras) para que ningún carácter Unicode «equivalga» a una letra del código.
    private static bool SameCode(string candidate, string code) =>
        CryptographicOperations.FixedTimeEquals(LowerAscii(candidate), LowerAscii(code));

    private static byte[] LowerAscii(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        for (var i = 0; i < bytes.Length; i++)
        {
            if (bytes[i] is >= (byte)'A' and <= (byte)'Z') bytes[i] += 32;
        }
        return bytes;
    }
}
