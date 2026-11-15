namespace DmCompanion.Core.Combat;

/// <summary>Campos de combate de un token en un instante: lo que deshacer devuelve a su sitio. Null en una lista o valor opcional significa que el token no lo tenía.</summary>
public sealed record TokenCombatFields(
    string TokenId,
    int? HpCurrent,
    int? HpTemp,
    IReadOnlyList<TokenCondition>? Conditions,
    DeathSaves? DeathSaves,
    bool? Dead);

/// <summary>Copia del combate y de los campos de combate de todos los tokens. En las entradas del historial, Combat va sin Log; en MapState.LastCombat, con él.</summary>
public sealed record CombatSnapshot(CombatState? Combat, IReadOnlyList<TokenCombatFields> Tokens);
