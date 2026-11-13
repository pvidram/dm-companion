namespace DmCompanion.Core.Vault;

public enum EntityType { Note, Session, Pc, Npc, Location, Map, Rule, Monster, Campaign }

public static class EntityTypes
{
    private static readonly Dictionary<string, EntityType> ByKey =
        Enum.GetValues<EntityType>().ToDictionary(ToKey, type => type, StringComparer.OrdinalIgnoreCase);

    public static EntityType Parse(string? raw) =>
        raw is not null && ByKey.TryGetValue(raw.Trim(), out var type) ? type : EntityType.Note;

    public static string ToKey(EntityType type) => type.ToString().ToLowerInvariant();
}
