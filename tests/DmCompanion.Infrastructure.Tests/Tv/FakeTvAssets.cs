using System.Text;
using DmCompanion.Core.Tv;

namespace DmCompanion.Infrastructure.Tests.Tv;

internal sealed class FakeTvAssets : ITvAssets
{
    public const string IndexHtml = "<!doctype html><title>TV</title>";

    public Stream? Open(string name) => name switch
    {
        "index.html" => new MemoryStream(Encoding.UTF8.GetBytes(IndexHtml)),
        "tv-app.js" => new MemoryStream(Encoding.UTF8.GetBytes("void 0;")),
        _ => null,
    };

    public string ContentType(string name) => name.EndsWith(".html", StringComparison.Ordinal) ? "text/html; charset=utf-8" : "text/javascript";
}
