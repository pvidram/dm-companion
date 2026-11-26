using DmCompanion.Core.Journal;
using DmCompanion.Core.Vault;
using DmCompanion.Core.Vault.Storage;
using Microsoft.Extensions.Logging;

namespace DmCompanion.Screens.Services;

public enum JournalLogStatus { Generated, ForeignNote, ModifiedOnDisk, NoVault, Failed }

/// <param name="Entries">Entradas de la bitácora generada.</param>
/// <param name="Created">True si la nota no existía; false si se sobrescribió una generada antes.</param>
public sealed record JournalLogResult(JournalLogStatus Status, int Entries = 0, bool Created = false);

/// <summary>
/// Genera <c>Diario de la campaña.md</c> en la raíz del vault con todas las entradas del índice vivo (spec §4.2). Solo escribe
/// con precondición y solo sobrescribe una nota que la propia app generó (<c>generated: true</c>).
/// </summary>
public sealed class JournalLogService(VaultHost host, JournalIndex index, TimeProvider time, ILogger<JournalLogService> logger)
{
    public const string ForeignNoteText = "Ya existe una nota «Diario de la campaña» que no generó la app. Renómbrala para generar la bitácora";

    private readonly SemaphoreSlim _gate = new(1, 1);

    public static string GeneratedText(int entries) => entries == 1 ? "Bitácora generada: 1 entrada" : $"Bitácora generada: {entries} entradas";

    public async Task<JournalLogResult> GenerateAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            return host.Current is { } vault ? await GenerateCoreAsync(vault, ct) : new JournalLogResult(JournalLogStatus.NoVault);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<JournalLogResult> GenerateCoreAsync(VaultService vault, CancellationToken ct)
    {
        VaultFileContent? existing;
        try
        {
            existing = await vault.ReadRawAsync(JournalPaths.LogPath, ct);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            existing = null; // lo normal la primera vez
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogError("No se pudo leer la bitácora existente ({ExceptionType}).", ex.GetType().FullName);
            return new JournalLogResult(JournalLogStatus.Failed);
        }

        var decision = JournalLog.Decide(existing?.Content);
        if (decision == JournalLogDecision.ForeignNote) return new JournalLogResult(JournalLogStatus.ForeignNote);

        try
        {
            // Con las imágenes ya listadas, «existe» es exacto y Render omite solo las que faltan de verdad.
            await index.RefreshImagesAsync(ct);
            var entries = index.Entries;
            var text = JournalLog.Render(entries, time.GetLocalNow());
            var precondition = decision == JournalLogDecision.Create
                ? WritePrecondition.MustNotExist
                : WritePrecondition.UnchangedSince(existing!.LastWriteTimeUtc);
            var saved = await vault.SaveAsync(JournalPaths.LogPath, text, precondition, ct);
            return saved.Status == WriteStatus.Written
                ? new JournalLogResult(JournalLogStatus.Generated, entries.Count, decision == JournalLogDecision.Create)
                : new JournalLogResult(JournalLogStatus.ModifiedOnDisk);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogError("No se pudo escribir la bitácora ({ExceptionType}).", ex.GetType().FullName);
            return new JournalLogResult(JournalLogStatus.Failed);
        }
    }
}
