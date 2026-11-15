namespace DmCompanion.Core.Combat;

/// <summary>Participante con su iniciativa tal como la apunta el DM; null si aún no se ha tirado.</summary>
public sealed record InitiativeEntry(string TokenId, int? Initiative);
