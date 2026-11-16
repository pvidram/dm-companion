namespace DmCompanion.Core.Glossary;

/// <summary>Casillas por lado que ocupa en el mapa una criatura del SRD según su tamaño.</summary>
public static class CreatureTokenSize
{
    /// <summary>Usa el primer tamaño: Tiny/Small/Medium → 1, Large → 2, Huge/Gargantuan → 3 (desconocido o vacío → 1).</summary>
    public static int Of(IReadOnlyList<string> sizeKeys) => sizeKeys.FirstOrDefault() switch
    {
        "Large" => 2,
        "Huge" or "Gargantuan" => 3,
        _ => 1,
    };
}
