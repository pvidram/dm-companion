namespace DmCompanion.Core.GameSystems;

/// <summary>
/// Cómo obtiene un sistema el máximo de vida de una nota. Label es el texto de la fila de la tarjeta;
/// StatId, la stat evaluada que esa fila sustituye (para no mostrar los HP dos veces).
/// </summary>
public sealed record HitPointsRule(string Label, string StatId, Func<StatValues, int?> Max)
{
    /// <summary>Regla de una stat numérica: su valor evaluado es el máximo.</summary>
    public static HitPointsRule FromStat(string label, string statId) =>
        new(label, statId, values => values.ById.TryGetValue(statId, out var stat) ? stat.Number : null);
}
