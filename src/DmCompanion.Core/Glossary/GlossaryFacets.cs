namespace DmCompanion.Core.Glossary;

/// <summary>Facetas para filtrar sin cargar el texto.</summary>
public sealed record GlossaryFacets(
    string? Cr = null, string? CreatureType = null,
    int? SpellLevel = null, IReadOnlyList<string>? Classes = null, bool Ritual = false, bool Concentration = false,
    IReadOnlyList<string>? Rarities = null);
