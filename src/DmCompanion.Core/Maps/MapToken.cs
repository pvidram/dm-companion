using System.Text.Json.Serialization;

namespace DmCompanion.Core.Maps;

/// <summary>Token del mapa. X e Y son el centro, en píxeles del mapa. Ac y Hp (≥ 0) solo existen en los genéricos. HpCurrent (opcional, ≥ 0) son los HP actuales; sin él, el token está al máximo.</summary>
public sealed record MapToken(
    string Id,
    string Name,
    string? Source,
    TokenSide Side,
    int Size,
    double X,
    double Y,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Ac,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Hp,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? HpCurrent = null);
