using DmCompanion.Core.GameSystems;

namespace DmCompanion.Core.Vault;

public static class NoteTemplates
{
    public static string Create(EntityType type, string title, IGameSystem system, string? imagePath = null)
    {
        var header = $"---\ntype: {EntityTypes.ToKey(type)}\nname: {Quote(title)}\n";
        if (type == EntityType.Map && imagePath is not null)
        {
            header += $"image: {Quote(imagePath)}\n";
        }

        var extra = type switch
        {
            EntityType.Pc => system.StatsTemplate(EntityType.Pc),
            EntityType.Npc => "secret: false\n" + system.StatsTemplate(EntityType.Npc),
            EntityType.Campaign => "system: dnd5e\n",
            _ => string.Empty,
        };
        var body = type switch
        {
            EntityType.Pc => "## Trasfondo\n\n## Notas\n",
            EntityType.Npc => "## Descripción\n\n%% Notas solo para el DM %%\n",
            EntityType.Location => "## Descripción\n\n## Lugares de interés\n",
            EntityType.Session => "## Resumen\n\n## Pendiente\n",
            EntityType.Campaign => "## Resumen\n",
            _ => string.Empty,
        };
        return header + extra.Replace("\r\n", "\n") + "---\n" + body;
    }

    public static string DefaultFolder(EntityType type) => type switch
    {
        EntityType.Campaign => string.Empty,
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
