using DmCompanion.Core.Maps;
using DmCompanion.Core.Vault;
using DmCompanion.Core.Vault.Storage;
using DmCompanion.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;

namespace DmCompanion.Screens.Tests;

// Vault sobre el storage real que cuenta, retrasa o hace fallar las operaciones sobre el `.state.json` (ver CountingStorage).
internal sealed class CountingFactory : IVaultServiceFactory
{
    public List<string> Writes { get; } = [];
    public int WritesStarted;
    public TimeSpan WriteDelay;
    public TimeSpan ReadDelay;
    // Si se asigna, la siguiente lectura del `.state.json` espera a que esta tarea termine (retención determinista, sin tiempos).
    public volatile Task? ReadGate;
    public int ReadsStarted;
    public volatile bool FailWrites;
    public volatile bool CrashWrites;
    public volatile Exception? ReadFailure;
    public volatile Exception? ListFailure;
    public volatile string? DeleteAfterNextStateStat;
    // Se ejecuta una vez justo antes de la siguiente escritura del `.state.json`, con el gate de la sesión tomado: el watcher no puede adelantarse.
    public volatile Action? BeforeNextStateWrite;
    public TimeSpan WriteResultSkew;
    public int StateStats;
    public int FileListings;
    // Completa ListFilesAsync antes de devolver la tarea: reproduce un listado que acaba antes de que el llamador llegue a su await.
    public volatile bool ListSynchronously;
    // Retrasa solo el listado de la tienda de audio (escenas y favoritos), para que su aviso llegue con la prueba ya en marcha.
    public TimeSpan ListDelay;
    public List<WriteMode> StatePreconditions { get; } = [];

    public VaultService Create(string rootPath) => new(
        new CountingStorage(new FileSystemVaultStorage(rootPath), this),
        new FileSystemVaultWatcher(rootPath),
        NullLogger<VaultService>.Instance);
}
