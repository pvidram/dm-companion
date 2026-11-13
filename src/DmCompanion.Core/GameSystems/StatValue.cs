namespace DmCompanion.Core.GameSystems;

public enum StatSource { Frontmatter, Derived, Manual, Missing }

/// <summary>Display ya formateado. Number: valor numérico si lo hay. Reason: motivo de «—».</summary>
public sealed record StatValue(string Id, string Display, int? Number, StatSource Source, string? Reason = null);
