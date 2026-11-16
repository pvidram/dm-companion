using DmCompanion.Core.Vault;

namespace DmCompanion.Core.Glossary;

public enum TokenSourceKind { Generic, VaultNote, Srd, Missing }

public sealed record TokenSource(TokenSourceKind Kind, VaultEntity? Note = null, GlossaryEntry? Entry = null)
{
    /// <summary>Frontmatter con el que evaluar stats: el de la nota, el adaptado de la criatura del SRD, o null.</summary>
    public IReadOnlyDictionary<string, object?>? Frontmatter => Kind switch
    {
        TokenSourceKind.VaultNote when Note is not null => Note.Frontmatter,
        TokenSourceKind.Srd when Entry?.Creature is { } creature => CreatureFrontmatter.From(creature),
        _ => null,
    };
}

public static class TokenSourceResolver
{
    public static bool IsSrd(string? source) => source?.StartsWith(GlossaryPacks.SrdPrefix, StringComparison.Ordinal) == true;

    public static TokenSource Resolve(string? source, VaultIndex vault, Func<string, GlossaryEntry?> srd)
    {
        if (string.IsNullOrWhiteSpace(source)) return new TokenSource(TokenSourceKind.Generic);
        if (IsSrd(source))
            return srd(source) is { } entry ? new TokenSource(TokenSourceKind.Srd, Entry: entry) : new TokenSource(TokenSourceKind.Missing);
        return vault.Get(source) is { } note ? new TokenSource(TokenSourceKind.VaultNote, Note: note) : new TokenSource(TokenSourceKind.Missing);
    }
}
