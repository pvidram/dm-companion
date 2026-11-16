using System.Text.Json.Serialization;

namespace DmCompanion.Core.Maps;

/// <summary>Zona de niebla: polígono en coordenadas del mapa (lista plana x, y, x, y…). Nace tapada; Revealed=false se guarda como null.</summary>
public sealed record MapFogZone(string Id, IReadOnlyList<double> Points, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? Revealed = null)
{
    [JsonIgnore]
    public bool IsRevealed => Revealed == true;
}
