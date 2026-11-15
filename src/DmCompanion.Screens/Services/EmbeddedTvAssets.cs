using DmCompanion.Core.Tv;

namespace DmCompanion.Screens.Services;

/// <summary>
/// Página de la TV embebida en la DLL (en Android no hay wwwroot servible fuera del WebView).
/// Solo se sirven los nombres de esta lista; cada uno es un recurso con nombre lógico <c>tv/{nombre}</c>.
/// </summary>
public sealed class EmbeddedTvAssets : ITvAssets
{
    private static readonly Dictionary<string, string> Known = new(StringComparer.Ordinal)
    {
        ["index.html"] = "text/html; charset=utf-8",
        ["tv.css"] = "text/css; charset=utf-8",
        ["tv-app.js"] = "text/javascript; charset=utf-8",
        ["tv-layout.js"] = "text/javascript; charset=utf-8",
        ["tv-strip.js"] = "text/javascript; charset=utf-8",
        ["tv-map.js"] = "text/javascript; charset=utf-8",
        ["token-opacity.js"] = "text/javascript; charset=utf-8",
        ["konva.min.js"] = "text/javascript; charset=utf-8",
        ["eb-garamond-latin-600-normal.woff2"] = "font/woff2",
        ["source-sans-3-latin-400-normal.woff2"] = "font/woff2",
        ["source-sans-3-latin-600-normal.woff2"] = "font/woff2",
    };

    public Stream? Open(string name) =>
        Known.ContainsKey(name) ? typeof(EmbeddedTvAssets).Assembly.GetManifestResourceStream("tv/" + name) : null;

    public string ContentType(string name) => Known.GetValueOrDefault(name, "application/octet-stream");
}
