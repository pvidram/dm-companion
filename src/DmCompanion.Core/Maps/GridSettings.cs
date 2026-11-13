namespace DmCompanion.Core.Maps;

public sealed record GridSettings(bool Enabled, double CellSize, double OffsetX, double OffsetY)
{
    public static GridSettings Default { get; } = new(false, 70, 0, 0);
}
