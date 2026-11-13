namespace DmCompanion.Core.Maps;

/// <summary>Cambios a un token. Name, Ac y Hp se ignoran en los tokens con Source.</summary>
public sealed record TokenEdit(TokenSide Side, int Size, string? Name, int? Ac, int? Hp);
