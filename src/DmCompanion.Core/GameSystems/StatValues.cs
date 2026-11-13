namespace DmCompanion.Core.GameSystems;

public sealed record StatValues(IReadOnlyDictionary<string, StatValue> ById, IReadOnlyList<string> Warnings)
{
    public static StatValues Empty { get; } = new(new Dictionary<string, StatValue>(), []);
}
