namespace DmCompanion.Core.Glossary;

public static class GlossaryPacks
{
    public const string SrdPrefix = "srd:";
    public const string VaultPrefix = "vault:";
    public const string IndexPack = "indice";

    public static string PackOf(GlossaryCategory category) => category switch
    {
        GlossaryCategory.Rule => "reglas",
        GlossaryCategory.Condition => "estados",
        GlossaryCategory.Creature => "criaturas",
        GlossaryCategory.Spell => "conjuros",
        GlossaryCategory.MagicItem => "objetos",
        GlossaryCategory.Equipment => "equipo",
        GlossaryCategory.CharacterOption => "personajes",
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, null),
    };

    public static IReadOnlyList<GlossaryCategory> All { get; } = Enum.GetValues<GlossaryCategory>();

    public static string SrdKey(GlossaryCategory category, string id) => $"{SrdPrefix}{PackOf(category)}/{id}";

    public static string VaultKey(string relativePath) => VaultPrefix + relativePath;

    /// <summary>Categoría de una clave srd:paquete/id; null si no es del SRD o el paquete no existe.</summary>
    public static GlossaryCategory? CategoryOfKey(string key)
    {
        if (!key.StartsWith(SrdPrefix, StringComparison.Ordinal)) return null;
        var slash = key.IndexOf('/', SrdPrefix.Length);
        if (slash < 0) return null;
        var pack = key.AsSpan(SrdPrefix.Length, slash - SrdPrefix.Length);
        foreach (var category in All)
            if (pack.SequenceEqual(PackOf(category))) return category;
        return null;
    }
}
