namespace DmCompanion.Core.Vault;

public sealed record VaultEntity(
    string RelativePath,
    string Title,
    EntityType Type,
    IReadOnlyDictionary<string, object?> Frontmatter,
    string Body,
    IReadOnlyList<Wikilink> Links,
    string? ParseError,
    DateTime LastWriteTimeUtc)
{
    public string LinkKey => VaultPaths.LinkKey(RelativePath);
    public string FileTitle => VaultPaths.FileTitle(RelativePath);

    // Fail-safe: con YAML inválido el parser devuelve campos vacíos y se perdería `secret: true`.
    public bool IsSecret => ParseError is not null || SecretFilter.IsSecret(Frontmatter);

    public string? Portrait => FrontmatterReader.GetString(Frontmatter, "portrait");
    public string? Alias => FrontmatterReader.GetString(Frontmatter, "alias");
    public string? Player => FrontmatterReader.GetString(Frontmatter, "player");
    public string Initial => Title.Length > 0 ? char.ToUpperInvariant(Title[0]).ToString() : "?";
}

public static class VaultEntityFactory
{
    public static VaultEntity Create(string relativePath, string content, DateTime lastWriteTimeUtc)
    {
        var path = VaultPaths.Normalize(relativePath);
        var frontmatter = FrontmatterParser.Parse(content);
        return new VaultEntity(
            path,
            FrontmatterReader.GetString(frontmatter.Fields, "name") ?? VaultPaths.FileTitle(path),
            EntityTypes.Parse(FrontmatterReader.GetString(frontmatter.Fields, "type")),
            frontmatter.Fields,
            frontmatter.Body,
            WikilinkExtractor.Extract(frontmatter.Body),
            frontmatter.Error,
            lastWriteTimeUtc);
    }
}
