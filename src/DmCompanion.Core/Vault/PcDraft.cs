using DmCompanion.Core.GameSystems;

namespace DmCompanion.Core.Vault;

/// <summary>Datos para crear la nota de un personaje jugador.</summary>
public sealed record PcDraft(string Name, string? Alias, string? Player, IReadOnlyDictionary<string, int> Stats)
{
    public const string NameRequiredMessage = "Escribe un nombre.";

    public IReadOnlyList<string> Validate(IGameSystem system)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(Name)) errors.Add(NameRequiredMessage);
        foreach (var field in system.PcCreationFields)
        {
            if (Stats.TryGetValue(field.Id, out var value) && field.Check(value) is { } error) errors.Add(error);
        }
        return errors;
    }
}
