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

    public static string CreatePc(PcDraft draft, IGameSystem system)
    {
        var header = $"---\ntype: pc\nname: {Quote(OneLine(draft.Name))}\n";
        if (!string.IsNullOrWhiteSpace(draft.Alias)) header += $"alias: {Quote(OneLine(draft.Alias))}\n";
        if (!string.IsNullOrWhiteSpace(draft.Player)) header += $"player: {Quote(OneLine(draft.Player))}\n";
        return header + system.PcStatsYaml(draft.Stats).Replace("\r\n", "\n") + "---\n## Trasfondo\n\n## Notas\n";
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

    private static string OneLine(string value) =>
        new string(value.Select(c => char.IsControl(c) ? ' ' : c).ToArray()).Trim();

    private static string Quote(string value) => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
}

public static class NoteFileName
{
    public const int MaxLength = 120;

    private static readonly char[] Invalid = ['\\', '/', ':', '*', '?', '"', '<', '>', '|', '#', '^', '[', ']'];

    private static readonly HashSet<string> Reserved = new(
        ["CON", "PRN", "AUX", "NUL", .. Enumerable.Range(1, 9).Select(i => $"COM{i}"), .. Enumerable.Range(1, 9).Select(i => $"LPT{i}")],
        StringComparer.OrdinalIgnoreCase);

    /// <summary>Nombre reservado de Windows: la parte antes del primer punto es CON, NUL, COM1…</summary>
    public static bool IsReserved(string name)
    {
        var dot = name.IndexOf('.');
        return Reserved.Contains(dot < 0 ? name : name[..dot]);
    }

    public static string FromTitle(string title)
    {
        var cleaned = Tidy(new string(title.Where(c => !Invalid.Contains(c) && !char.IsControl(c)).ToArray()));
        if (cleaned.Length > MaxLength) cleaned = Tidy(cleaned[..MaxLength]);
        if (cleaned.Length == 0) return "Sin título";
        if (!IsReserved(cleaned)) return cleaned;
        var dot = cleaned.IndexOf('.');
        return dot < 0 ? cleaned + "_" : cleaned[..dot] + "_" + cleaned[dot..];
    }

    private static string Tidy(string value) => value.Trim().TrimStart('.').TrimEnd('.').Trim();
}
