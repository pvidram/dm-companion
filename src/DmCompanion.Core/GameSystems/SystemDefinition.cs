namespace DmCompanion.Core.GameSystems;

public sealed record SystemDefinition(IReadOnlyList<StatGroup> Groups, IReadOnlyList<StatDefinition> Stats)
{
    public static SystemDefinition Empty { get; } = new([], []);
}
