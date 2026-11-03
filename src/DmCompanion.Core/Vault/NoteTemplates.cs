namespace DmCompanion.Core.Vault;

public static class NoteTemplates
{
    private const string CharacterStats = """
        portrait:
        stats:
          ac: 10
          hp: 10
          speed: 30
          initiative_bonus: 0
          abilities: { str: 10, dex: 10, con: 10, int: 10, wis: 10, cha: 10 }

        """;

    public static string Create(EntityType type, string title)
    {
        var header = $"---\ntype: {EntityTypes.ToKey(type)}\nname: {Quote(title)}\n";
        var extra = type switch
        {
            EntityType.Pc => CharacterStats,
            EntityType.Npc => "secret: false\n" + CharacterStats,
            _ => string.Empty,
        };
        var body = type switch
        {
            EntityType.Pc => "## Trasfondo\n\n## Notas\n",
            EntityType.Npc => "## Descripción\n\n%% Notas solo para el DM %%\n",
            EntityType.Location => "## Descripción\n\n## Lugares de interés\n",
            EntityType.Session => "## Resumen\n\n## Pendiente\n",
            _ => string.Empty,
        };
        return header + extra.Replace("\r\n", "\n") + "---\n" + body;
    }

    public static string DefaultFolder(EntityType type) => type switch
    {
        EntityType.Pc or EntityType.Npc => "Personajes",
        EntityType.Location => "Lugares",
        EntityType.Map => "Mapas",
        EntityType.Rule or EntityType.Monster => "Glosario",
        _ => "Historia",
    };

    private static string Quote(string value) => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
}

public static class NoteFileName
{
    private static readonly char[] Invalid = ['\\', '/', ':', '*', '?', '"', '<', '>', '|', '#', '^', '[', ']'];

    public static string FromTitle(string title)
    {
        var cleaned = new string(title.Where(c => !Invalid.Contains(c) && !char.IsControl(c)).ToArray()).Trim().TrimStart('.').TrimEnd('.').Trim();
        return cleaned.Length == 0 ? "Sin título" : cleaned;
    }
}
