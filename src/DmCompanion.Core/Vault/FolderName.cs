namespace DmCompanion.Core.Vault;

public sealed record FolderNameCheck(string? Name, string? Error)
{
    public bool IsValid => Name is not null;
}

public static class FolderName
{
    private static readonly char[] Invalid = ['/', '\\', ':', '*', '?', '"', '<', '>', '|'];

    public static FolderNameCheck Check(string raw)
    {
        var name = (raw ?? string.Empty).Trim();
        if (name.Length == 0) return Fail("Escribe un nombre para la carpeta.");
        var badIndex = name.IndexOfAny(Invalid);
        if (badIndex >= 0) return Fail($"El nombre no puede contener «{name[badIndex]}».");
        if (name.Any(char.IsControl)) return Fail("El nombre no puede contener caracteres de control.");

        if (name.StartsWith('.')) return Fail("El nombre no puede empezar por un punto.");
        if (name.EndsWith('.')) return Fail("El nombre no puede terminar en un punto.");
        if (NoteFileName.IsReserved(name)) return Fail("Ese nombre está reservado por Windows.");
        return new FolderNameCheck(name, null);
    }

    private static FolderNameCheck Fail(string error) => new(null, error);
}
