namespace DmCompanion.Core.Vault.Storage;

public enum CreateVaultStatus { Created, InvalidName, FolderMissing, DriveRoot, NoteExists, ObsidianExists, PartiallyCreated, Failed }

/// <summary>Resultado de crear un vault. Message: texto en español en cualquier estado distinto de Created.</summary>
public sealed record CreateVaultResult(CreateVaultStatus Status, string? Message = null);

public interface IVaultCreator
{
    /// <summary>Crea en una carpeta existente la nota de campaña y `.obsidian/app.json`, sin pisar nada.
    /// Solo rechaza las raíces de unidad (DriveRoot). Las raíces de la plataforma y las carpetas no vacías las filtra la UI; esta validación es defensa en profundidad, no la única barrera.</summary>
    Task<CreateVaultResult> CreateAsync(string folderPath, string campaignName, string systemId, CancellationToken ct = default);
}
