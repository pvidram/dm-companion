using DmCompanion.Core.Maps;

namespace DmCompanion.Core.GameSystems;

/// <summary>Chebyshev: cada diagonal cuenta como una casilla.</summary>
public enum DiagonalRule { Chebyshev }

public sealed record GridRules(GridUnit DefaultUnit, DiagonalRule Diagonals)
{
    public static GridRules Feet5 { get; } = new(new GridUnit(5, "pies"), DiagonalRule.Chebyshev);
}
