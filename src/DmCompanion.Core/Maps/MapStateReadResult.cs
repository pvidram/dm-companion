namespace DmCompanion.Core.Maps;

/// <summary>Repairs es el número de elementos incoherentes del combate que se corrigieron al leer.</summary>
public sealed record MapStateReadResult(MapState State, MapStateReadStatus Status, int Repairs = 0);
