namespace DmCompanion.Core.GameSystems;

/// <summary>
/// Key: ruta con puntos dentro del frontmatter ("stats.ac"). Abbreviation: sigla que acompaña a la etiqueta
/// en grupos alfabéticos. Para Derived, Key es la ruta del valor manual (si viene, manda).
/// </summary>
public sealed record StatDefinition(string Id, string Key, string Label, StatKind Kind, string GroupId,
    bool ShowInQuickCard = false, string? Abbreviation = null);
