using System.Text.Json.Serialization;
using DmCompanion.Core.Combat;

namespace DmCompanion.Core.Maps;

/// <summary>Token del mapa. X e Y son el centro, en píxeles del mapa. Ac y Hp (≥ 0) solo existen en los genéricos. HpCurrent (opcional, ≥ 0) son los HP actuales; sin él, el token está al máximo. HpTemp (≥ 0), Conditions, DeathSaves y Dead (muerto: la tira de turnos lo salta) son del combate y se omiten si no hay.</summary>
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
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? HpCurrent = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? HpTemp = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<TokenCondition>? Conditions = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DeathSaves? DeathSaves = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? Dead = null)
{
    [JsonIgnore]
    public bool IsDead => Dead == true;

    [JsonIgnore]
    public int TempHp => HpTemp ?? 0;

    [JsonIgnore]
    public IReadOnlyList<TokenCondition> ConditionList => Conditions ?? [];
}
