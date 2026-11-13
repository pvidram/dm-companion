namespace DmCompanion.Core.GameSystems;

public sealed class GameSystemRegistry
{
    private const string DefaultId = "dnd5e";
    private readonly Dictionary<string, IGameSystem> _byId;

    private GameSystemRegistry(IReadOnlyList<IGameSystem> systems)
    {
        All = systems;
        _byId = systems.ToDictionary(s => s.Id, StringComparer.OrdinalIgnoreCase);
    }

    public static GameSystemRegistry Default { get; } = new([new Dnd5eSystem(), new CosmereSystem()]);

    public IReadOnlyList<IGameSystem> All { get; }

    public IGameSystem Get(string? id)
    {
        var key = string.IsNullOrWhiteSpace(id) ? DefaultId : id.Trim();
        return _byId.TryGetValue(key, out var system) ? system : new GenericSystem(key);
    }
}
