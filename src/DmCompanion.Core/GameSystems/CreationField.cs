namespace DmCompanion.Core.GameSystems;

/// <summary>Una estadística base que se pide al crear un PJ (con su rango válido).</summary>
public sealed record CreationField(string Id, string Label, int Default, int Min, int Max, string? Group = null)
{
    /// <summary>Null si el valor está en rango; si no, el aviso que nombra el campo y el rango.</summary>
    public string? Check(int value) =>
        value >= Min && value <= Max ? null : $"{Label}: debe estar entre {Min} y {Max}.";

    public static IReadOnlyDictionary<string, int> Defaults(IReadOnlyList<CreationField> fields) =>
        fields.ToDictionary(f => f.Id, f => f.Default);
}
