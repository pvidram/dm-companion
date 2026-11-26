using System.Text.Json;
using System.Text.Json.Serialization;
using DmCompanion.Screens.Platform;
using DmCompanion.UI.Interop;
using Microsoft.Extensions.Logging;

namespace DmCompanion.Screens.Services;

/// <summary>Persists per-device panel sizes, pins and corner anchors, one JSON entry per key.</summary>
public sealed class PanelLayoutStore(IAppPreferences preferences, ILogger<PanelLayoutStore> logger)
{
    private const string Prefix = "layout.";
    private readonly HashSet<string> _warned = [];

    public WindowSize? GetSize(string key)
    {
        var e = Read(key);
        double? w = e.W is > 0 ? e.W : null, h = e.H is > 0 ? e.H : null;
        return w is null && h is null ? null : new WindowSize(w, h);
    }

    /// <summary>Guarda los ejes elegidos: un eje con valor positivo sustituye al guardado y uno null o no válido lo deja como estaba.</summary>
    public void SetSize(string key, WindowSize size)
    {
        var entry = Read(key);
        Write(key, entry with
        {
            W = size.Width is > 0 ? size.Width : entry.W,
            H = size.Height is > 0 ? size.Height : entry.H,
        });
    }

    public void ResetSize(string key) => Write(key, Read(key) with { W = null, H = null });

    public bool IsPinned(string key) => Read(key).Pinned == true;

    public void SetPinned(string key, bool pinned) => Write(key, Read(key) with { Pinned = pinned ? true : null });

    public WindowAnchor? GetAnchor(string key)
    {
        var a = Read(key).Anchor;
        var corner = a?.Corner switch
        {
            "tl" => WindowCorner.TopLeft,
            "tr" => WindowCorner.TopRight,
            "bl" => WindowCorner.BottomLeft,
            "br" => WindowCorner.BottomRight,
            _ => (WindowCorner?)null,
        };
        return corner is null ? null : new WindowAnchor(corner.Value, a!.Dx, a.Dy);
    }

    public void SetAnchor(string key, WindowAnchor? anchor)
    {
        var dto = anchor is null ? null : new AnchorDto(anchor.Corner switch
        {
            WindowCorner.TopLeft => "tl",
            WindowCorner.TopRight => "tr",
            WindowCorner.BottomLeft => "bl",
            _ => "br",
        }, anchor.OffsetX, anchor.OffsetY);
        Write(key, Read(key) with { Anchor = dto });
    }

    private Entry Read(string key)
    {
        var raw = preferences.Get(Prefix + key);
        if (string.IsNullOrWhiteSpace(raw)) return new Entry();
        try
        {
            return JsonSerializer.Deserialize<Entry>(raw) ?? new Entry();
        }
        catch (JsonException ex)
        {
            if (_warned.Add(key)) logger.LogWarning(ex, "Layout entry {Key} is not valid JSON; ignoring it", key);
            return new Entry();
        }
    }

    private void Write(string key, Entry entry)
    {
        if (entry == new Entry())
        {
            preferences.Remove(Prefix + key);
            return;
        }
        preferences.Set(Prefix + key, JsonSerializer.Serialize(entry));
    }

    private sealed record AnchorDto(
        [property: JsonPropertyName("corner")] string? Corner,
        [property: JsonPropertyName("dx")] double Dx,
        [property: JsonPropertyName("dy")] double Dy);

    private sealed record Entry
    {
        [JsonPropertyName("w"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? W { get; init; }

        [JsonPropertyName("h"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? H { get; init; }

        [JsonPropertyName("pinned"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? Pinned { get; init; }

        [JsonPropertyName("anchor"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public AnchorDto? Anchor { get; init; }
    }
}
