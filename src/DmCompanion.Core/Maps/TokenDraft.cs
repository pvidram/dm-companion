namespace DmCompanion.Core.Maps;

/// <summary>Datos para crear un token. Source es la ruta de la nota si viene de una ficha; null si es genérico.</summary>
public sealed record TokenDraft(string Name, string? Source, TokenSide Side, int Size, int? Ac, int? Hp, string? Alias = null);
