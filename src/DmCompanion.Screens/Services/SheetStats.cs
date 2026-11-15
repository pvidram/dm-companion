using System.Globalization;
using DmCompanion.Core.GameSystems;

namespace DmCompanion.Screens.Services;

/// <summary>Una estadística tal como se ve en la ficha: etiqueta y valor ya formateado.</summary>
public sealed record SheetStat(string Label, string Value);

/// <summary>
/// Qué estadísticas muestra la ficha de PJ, en qué orden y con qué etiqueta. Lo comparten la ficha de la tablet
/// (<c>CharacterSheet</c>) y la de la TV, para que las dos enseñen lo mismo.
/// </summary>
public static class SheetStats
{
    private static readonly StringComparer SpanishOrder = StringComparer.Create(CultureInfo.GetCultureInfo("es-ES"), false);

    /// <summary>Estadísticas de ese grupo que tienen valor, en el orden del grupo.</summary>
    public static IEnumerable<StatDefinition> StatsOf(IGameSystem system, StatValues values, StatGroup group)
    {
        var stats = system.Definition.Stats.Where(s => s.GroupId == group.Id && values.ById.ContainsKey(s.Id));
        return group.Order == GroupOrder.Alphabetical ? stats.OrderBy(s => s.Label, SpanishOrder) : stats;
    }

    public static string LabelOf(StatGroup group, StatDefinition stat) =>
        group.Order == GroupOrder.Alphabetical && stat.Abbreviation is { } abbreviation ? $"{stat.Label} · {abbreviation}" : stat.Label;

    /// <summary>Todas las estadísticas de la ficha en el orden en que se ven; con un sistema desconocido, por su id.</summary>
    public static IReadOnlyList<SheetStat> For(IGameSystem system, StatValues values) =>
        system.IsKnown
            ? system.Definition.Groups
                .SelectMany(group => StatsOf(system, values, group).Select(stat => new SheetStat(LabelOf(group, stat), values.ById[stat.Id].Display)))
                .ToList()
            : values.ById.Values.Select(value => new SheetStat(value.Id, value.Display)).ToList();
}
