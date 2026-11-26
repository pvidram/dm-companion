using System.Text.Json.Serialization;

namespace DmCompanion.Core.Maps;

/// <summary>Zona de niebla: polígono en coordenadas del mapa (lista plana x, y, x, y…). Nace tapada; Revealed=false se guarda como null. Type ausente = total.</summary>
public sealed record MapFogZone(
    string Id,
    IReadOnlyList<double> Points,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? Revealed = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] FogType? Type = null)
{
    [JsonIgnore]
    public bool IsRevealed => Revealed == true;

    [JsonIgnore]
    public FogType EffectiveType => Type ?? FogType.Total;

    [JsonIgnore]
    public bool IsNpc => EffectiveType == FogType.Npc;
}
