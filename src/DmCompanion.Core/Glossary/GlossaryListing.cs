namespace DmCompanion.Core.Glossary;

/// <summary>Fila del índice: lo justo para listar, buscar y filtrar.</summary>
public sealed record GlossaryListing(
    string Key, GlossaryCategory Category, string? Subtype, string Name, string? NameEn, string Summary,
    GlossaryFacets Facets, string? VaultPath = null, string? Overrides = null)
{
    public bool IsVault => VaultPath is not null;
}
