using System.Text.Json;
using System.Text.Json.Nodes;

namespace DmCompanion.Core.Audio;

/// <summary>JSON de una escena (<c>_audio/_dm/scenes/*.json</c>). El nombre no se guarda: es el del fichero.</summary>
public static class AudioSceneSerializer
{
    private const int Version = 1;

    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    public static string Write(AudioScene scene)
    {
        var root = new JsonObject
        {
            ["version"] = Version,
            ["masterVolume"] = scene.MasterVolume,
            ["background"] = scene.Background is { } background ? WriteBackground(background) : null,
            ["effects"] = new JsonArray([.. scene.Effects.Select(WriteEffect)]),
        };
        return root.ToJsonString(WriteOptions);
    }

    /// <summary>JSON inválido → <c>(null, 0)</c>. Lo demás se repara y cada arreglo suma uno a <c>Repairs</c>.</summary>
    public static (AudioScene? Scene, int Repairs) Read(string json, string name)
    {
        JsonObject? root;
        try
        {
            root = JsonNode.Parse(json) as JsonObject;
        }
        catch (JsonException)
        {
            return (null, 0);
        }
        if (root is null) return (null, 0);

        var repairs = 0;
        var master = ReadVolume(root["masterVolume"], ref repairs);
        var background = ReadBackground(root["background"], ref repairs);
        var effects = new List<SceneEffect>();
        if (root["effects"] is JsonArray array)
        {
            foreach (var node in array)
                if (ReadEffect(node, ref repairs) is { } effect) effects.Add(effect);
        }
        return (new AudioScene(name, background, effects, master), repairs);
    }

    private static JsonObject WriteBackground(SceneBackground background)
    {
        var node = new JsonObject();
        switch (background.Source)
        {
            case TrackBackground track: node["track"] = track.Track; break;
            case SectionBackground section: node["section"] = section.Section; break;
        }
        node["volume"] = background.Volume;
        return node;
    }

    private static JsonObject WriteEffect(SceneEffect effect)
    {
        var repeat = effect.Repeat.Normalize();
        var repeatNode = new JsonObject { ["kind"] = KindName(repeat.Kind) };
        if (repeat.Kind == RepeatKind.RepeatWithPause)
        {
            repeatNode["minPause"] = repeat.MinPauseSeconds;
            repeatNode["maxPause"] = repeat.MaxPauseSeconds;
        }
        return new JsonObject { ["track"] = effect.Track, ["volume"] = effect.Volume, ["repeat"] = repeatNode };
    }

    private static SceneBackground? ReadBackground(JsonNode? node, ref int repairs)
    {
        if (node is not JsonObject obj) return null;
        BackgroundSource? source = Text(obj["track"]) is { } track ? new TrackBackground(track)
            : Text(obj["section"]) is { } section ? new SectionBackground(section)
            : null;
        return source is null ? null : new SceneBackground(source, ReadVolume(obj["volume"], ref repairs));
    }

    private static SceneEffect? ReadEffect(JsonNode? node, ref int repairs)
    {
        if (node is not JsonObject obj || Text(obj["track"]) is not { } track) return null;
        return new SceneEffect(track, ReadVolume(obj["volume"], ref repairs), ReadRepeat(obj["repeat"], ref repairs));
    }

    private static EffectRepeat ReadRepeat(JsonNode? node, ref int repairs)
    {
        if (node is not JsonObject obj) return EffectRepeat.Once;
        var kind = Text(obj["kind"])?.ToLowerInvariant() switch
        {
            "loop" => RepeatKind.Loop,
            "pause" => RepeatKind.RepeatWithPause,
            _ => RepeatKind.Once,
        };
        var raw = new EffectRepeat(kind, Int(obj["minPause"]), Int(obj["maxPause"]));
        var normalized = raw.Normalize();
        if (normalized != raw && kind == RepeatKind.RepeatWithPause) repairs++;
        return normalized;
    }

    private static double ReadVolume(JsonNode? node, ref int repairs)
    {
        if (node is not JsonValue value || !value.TryGetValue<double>(out var volume) || double.IsNaN(volume)) return 1;
        var clamped = Math.Clamp(volume, 0, 1);
        if (clamped != volume) repairs++;
        return clamped;
    }

    private static int Int(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<int>(out var number) ? number : 0;

    private static string? Text(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text) ? text : null;

    private static string KindName(RepeatKind kind) => kind switch
    {
        RepeatKind.Loop => "loop",
        RepeatKind.RepeatWithPause => "pause",
        _ => "once",
    };
}
