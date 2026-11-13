using System.Text.RegularExpressions;
using DmCompanion.Core.Maps;
using DmCompanion.Core.Vault;
using DmCompanion.Core.Vault.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DmCompanion.Screens.Services;

public enum MapOpStatus { Ok, NameTaken, InvalidName, UnsupportedImage, IoError }

public sealed record MapOpResult(MapOpStatus Status, string? NotePath)
{
    /// <summary>Texto para el usuario cuando la operación no salió bien; null si salió bien.</summary>
    public string? ErrorText => Status switch
    {
        MapOpStatus.NameTaken => "Ya existe un mapa o un fichero con ese nombre.",
        MapOpStatus.InvalidName => "El nombre no es válido: no puede estar vacío ni llevar / \\ : * ? \" < > | # ^ [ ] ni empezar por punto.",
        MapOpStatus.UnsupportedImage => "Formato de imagen no admitido. Formatos válidos: png, jpg, jpeg, webp, gif.",
        MapOpStatus.IoError => "No se pudo completar la operación en el vault. Inténtalo de nuevo.",
        _ => null,
    };
}

/// <summary>
/// Altas, bajas y renombrados de mapas en `Mapas/`. Los errores de contenido (nombre ocupado o inválido) y de IO
/// son resultados, nunca excepciones. Nunca toca notas que no sean `type: map`.
/// </summary>
public sealed partial class MapLibrary(VaultHost host, MapSession session, ILogger<MapLibrary>? logger = null)
{
    private readonly ILogger _log = (ILogger?)logger ?? NullLogger.Instance;

    private static readonly MapOpResult Invalid = new(MapOpStatus.InvalidName, null);
    private static readonly MapOpResult Unsupported = new(MapOpStatus.UnsupportedImage, null);
    private static readonly MapOpResult Taken = new(MapOpStatus.NameTaken, null);
    private static readonly MapOpResult Failed = new(MapOpStatus.IoError, null);

    [GeneratedRegex(@"^\.[a-z0-9]+$")]
    private static partial Regex ExtensionPattern();

    public async Task<MapCatalog> LoadAsync()
    {
        if (host.Current is not { } vault) return new MapCatalog([], []);
        try
        {
            var images = await vault.ListFilesAsync(MapPaths.IsImage);
            return MapCatalog.Build(vault.Index, images.Select(file => file.RelativePath));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return MapCatalog.Build(vault.Index, []);
        }
    }

    public Task<MapOpResult> CreateBlankAsync(string name) => GuardAsync(async vault =>
    {
        if (!TryName(name, out var clean)) return Invalid;
        var notePath = NotePathOf(clean);
        if (await vault.StatAsync(notePath) is not null) return Taken;
        return await CreateNoteAsync(vault, notePath, clean, imagePath: null);
    });

    public Task<MapOpResult> CreateFromImageAsync(string imagePath) => GuardAsync(async vault =>
    {
        if (!VaultPaths.TryNormalizeSafe(imagePath, out var image)
            || !image.StartsWith(MapPaths.MapsFolder + "/", StringComparison.OrdinalIgnoreCase)) return Invalid;
        // Mismo comparador que el catálogo, que lista huérfanas sin distinguir mayúsculas: lo que ofrece «Crear mapa» se puede crear.
        if (!MapPaths.IsImage(image)) return Unsupported;
        if (!TryName(Path.GetFileNameWithoutExtension(image), out var clean)) return Invalid;
        if (await vault.StatAsync(image) is null) return Failed;
        var notePath = NotePathOf(clean);
        if (await vault.StatAsync(notePath) is not null) return Taken;
        return await CreateNoteAsync(vault, notePath, clean, image);
    });

    public Task<MapOpResult> ImportAsync(string name, string extension, Stream content) => GuardAsync(async vault =>
    {
        if (!TryName(name, out var clean)) return Invalid;
        if (!TryExtension(extension, out var ext)) return Unsupported;
        var notePath = NotePathOf(clean);
        var imagePath = $"{MapPaths.MapsFolder}/{clean}{ext}";
        if (await vault.StatAsync(notePath) is not null || await vault.StatAsync(imagePath) is not null) return Taken;

        var written = await vault.WriteBytesAsync(imagePath, content, WritePrecondition.MustNotExist);
        if (written.Status != WriteStatus.Written) return Taken;

        // Sin nota la imagen recién escrita sería un huérfano que el usuario no pidió, falle por resultado o por excepción.
        MapOpResult result;
        try
        {
            result = await CreateNoteAsync(vault, notePath, clean, imagePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            await TryDeleteAsync(vault, imagePath);
            throw;
        }
        if (result.Status != MapOpStatus.Ok) await TryDeleteAsync(vault, imagePath);
        return result;
    });

    public Task<MapOpResult> RenameAsync(string notePath, string newName) => GuardAsync(async vault =>
    {
        if (!IsMap(vault, notePath)) return Invalid;
        if (!TryName(newName, out var clean)) return Invalid;

        var newPath = $"{FolderOf(notePath)}{clean}.md";
        var samePath = string.Equals(notePath, newPath, StringComparison.Ordinal);
        var onlyCase = !samePath && string.Equals(notePath, newPath, StringComparison.OrdinalIgnoreCase);
        var newState = MapPaths.StatePathFor(newPath);
        // Un cambio de solo mayúsculas apunta al mismo fichero en sistemas que no las distinguen: no cuenta como ocupado.
        if (!samePath && !onlyCase && (await vault.StatAsync(newPath) is not null || await vault.StatAsync(newState) is not null)) return Taken;

        var wasOpen = IsOpen(notePath);
        if (wasOpen && !await CloseOpenMapAsync(notePath)) return Failed;

        var result = Failed;
        try
        {
            result = await RenameClosedAsync(vault, notePath, newPath, clean, samePath);
        }
        finally
        {
            // Se reabre la ruta en la que la nota existe de verdad, también tras un fallo a medias.
            if (wasOpen) await session.OpenAsync(result.Status == MapOpStatus.Ok ? newPath : result.NotePath ?? notePath);
        }
        if (result.Status == MapOpStatus.Ok) session.RememberRenamed(notePath, newPath);
        return result;
    });

    public Task<MapOpResult> DeleteAsync(string notePath) => GuardAsync(async vault =>
    {
        if (!IsMap(vault, notePath)) return Invalid;
        if (IsOpen(notePath) && !await CloseOpenMapAsync(notePath)) return Failed;

        var statePath = MapPaths.StatePathFor(notePath);
        // La nota primero: si falla el borrado del estado queda un fichero suelto, no un mapa sin dibujos.
        await vault.DeleteAsync(notePath);
        if (await vault.StatAsync(statePath) is not null) await vault.DeleteAsync(statePath);
        session.ForgetDeleted(notePath);
        return new MapOpResult(MapOpStatus.Ok, null);
    });

    private async Task<MapOpResult> RenameClosedAsync(VaultService vault, string notePath, string newPath, string clean, bool samePath)
    {
        var content = (await vault.ReadRawAsync(notePath)).Content;
        var updated = MapNoteName.WithName(content, clean);
        if (samePath)
        {
            if (updated != content) await vault.SaveAsync(notePath, updated, WritePrecondition.Overwrite);
            return new MapOpResult(MapOpStatus.Ok, notePath);
        }

        if (!await vault.MoveAsync(notePath, newPath)) return Taken;
        var oldState = MapPaths.StatePathFor(notePath);
        var newState = MapPaths.StatePathFor(newPath);
        var stateMoved = false;
        var status = MapOpStatus.Ok;
        try
        {
            if (await vault.StatAsync(oldState) is not null)
            {
                stateMoved = await vault.MoveAsync(oldState, newState);
                if (!stateMoved) status = MapOpStatus.NameTaken;
            }
            if (status == MapOpStatus.Ok) await vault.SaveAsync(newPath, updated, WritePrecondition.Overwrite);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            status = MapOpStatus.IoError;
        }
        if (status == MapOpStatus.Ok) return new MapOpResult(MapOpStatus.Ok, newPath);

        // Se deshace lo movido, primero el estado. Si algo no vuelve, el resultado señala dónde está la nota ahora.
        if (stateMoved && !await TryMoveAsync(vault, newState, oldState)) return new MapOpResult(MapOpStatus.IoError, newPath);
        if (!await TryMoveAsync(vault, newPath, notePath)) return new MapOpResult(MapOpStatus.IoError, newPath);
        return new MapOpResult(status, notePath);
    }

    private async Task<bool> TryMoveAsync(VaultService vault, string from, string to)
    {
        try
        {
            if (await vault.MoveAsync(from, to)) return true;
            _log.LogError("No se pudo deshacer el renombrado de un mapa: el destino ya existe.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log.LogError("No se pudo deshacer el renombrado de un mapa ({ExceptionType}).", ex.GetType().FullName);
        }
        return false;
    }

    private async Task<MapOpResult> GuardAsync(Func<VaultService, Task<MapOpResult>> operation)
    {
        if (host.Current is not { } vault) return Failed;
        try
        {
            return await operation(vault);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Failed;
        }
    }

    private static async Task<MapOpResult> CreateNoteAsync(VaultService vault, string notePath, string name, string? imagePath)
    {
        var text = NoteTemplates.Create(EntityType.Map, name, vault.ActiveSystem, imagePath);
        var result = await vault.SaveAsync(notePath, text, WritePrecondition.MustNotExist);
        return result.Status switch
        {
            WriteStatus.Written => new MapOpResult(MapOpStatus.Ok, notePath),
            WriteStatus.AlreadyExists => Taken,
            _ => Failed,
        };
    }

    private static async Task TryDeleteAsync(VaultService vault, string path)
    {
        try
        {
            await vault.DeleteAsync(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Mejor esfuerzo: el resultado que se devuelve ya es el fallo original.
        }
    }

    // Cerrar guarda lo pendiente; si no pudo guardarse, el mapa sigue abierto y no se toca nada de él.
    private async Task<bool> CloseOpenMapAsync(string notePath)
    {
        await session.CloseAsync();
        return !IsOpen(notePath);
    }

    private bool IsOpen(string notePath) => string.Equals(session.Current?.NotePath, notePath, StringComparison.Ordinal);

    private static bool IsMap(VaultService vault, string notePath) =>
        vault.Index.Get(notePath) is { Type: EntityType.Map };

    private static string NotePathOf(string name) => $"{MapPaths.MapsFolder}/{name}.md";

    private static string FolderOf(string notePath) => notePath[..(notePath.LastIndexOf('/') + 1)];

    private static bool TryName(string? name, out string clean)
    {
        clean = name?.Trim() ?? string.Empty;
        return clean.Length > 0 && NoteFileName.FromTitle(clean) == clean;
    }

    private static bool TryExtension(string? extension, out string normalized)
    {
        var text = (extension ?? string.Empty).Trim().ToLowerInvariant();
        normalized = text.StartsWith('.') ? text : "." + text;
        return ExtensionPattern().IsMatch(normalized) && MapPaths.IsImage(normalized);
    }
}
