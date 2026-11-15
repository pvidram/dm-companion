namespace DmCompanion.Core.Combat;

/// <summary>Aviso no bloqueante para la UI; la UI pone los nombres a partir de los ids.</summary>
public abstract record CombatNotice(string TokenId);

/// <summary>Un estado del token ha agotado su duración (queda marcado como vencido, no se quita).</summary>
public sealed record ConditionExpired(string TokenId, string ConditionId) : CombatNotice(TokenId);

/// <summary>El token concentrado ha recibido daño: salvación de Constitución con esta CD.</summary>
public sealed record ConcentrationCheck(string TokenId, int Dc) : CombatNotice(TokenId);

/// <summary>El token no muerto ha llegado a 3 salvaciones de muerte fallidas: la UI propone marcarlo como muerto (la app nunca lo hace sola).</summary>
public sealed record ThreeFailures(string TokenId) : CombatNotice(TokenId);

/// <summary>Se ha terminado el combate: la UI ofrece deshacerlo durante unos segundos. No va ligado a ningún token.</summary>
public sealed record CombatEnded() : CombatNotice(string.Empty);
