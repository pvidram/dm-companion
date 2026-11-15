using System.Text.Json;
using System.Text.Json.Nodes;
using DmCompanion.Core.Audio;
using DmCompanion.Core.Vault;
using DmCompanion.Core.Vault.Storage;
using Microsoft.Extensions.Logging;

namespace DmCompanion.Screens.Services;

public enum StoreResult { Ok, AlreadyExists, NotFound, Failed }

/// <summary>Escenas y favoritos de <c>_audio/_dm/</c> del vault abierto; es lo único que la app escribe en <c>_audio/</c>.</summary>
public sealed partial class AudioStore : IDisposable
{
    private const string ScenesFolder = AudioPaths.AppFolder + "/scenes";
    private const string FavoritesPath = AudioPaths.AppFolder + "/favorites.json";
    private const string AppPrefix = AudioPaths.AppFolder + "/";

    private readonly VaultHost _host;
    private readonly ILogger<AudioStore> _logger;
    private readonly object _gate = new();
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private VaultService? _vault;
    private int _generation;
    private IReadOnlyDictionary<string, AudioScene> _scenes = new Dictionary<string, AudioScene>();
    private IReadOnlyList<string> _sceneNames = [];
    private IReadOnlyList<string> _favorites = [];
    // favorites.json existe pero no se pudo leer: escribir encima perdería la lista, así que no se toca hasta leerlo bien.
    private bool _favoritesUnreadable;

    public AudioStore(VaultHost host, ILogger<AudioStore> logger)
    {
        _host = host;
        _logger = logger;
        _host.Changed += OnHostChanged;
        Attach();
    }

    public IReadOnlyList<string> SceneNames { get { lock (_gate) return _sceneNames; } }

    public IReadOnlyList<string> Favorites { get { lock (_gate) return _favorites; } }

    public event EventHandler? Changed;

    public void Dispose()
    {
        _host.Changed -= OnHostChanged;
        lock (_gate)
        {
            _generation++;
            if (_vault is { } vault) vault.FileChanged -= OnFileChanged;
            _vault = null;
        }
    }

    public Task<AudioScene?> LoadSceneAsync(string name)
    {
        lock (_gate)
        {
            return Task.FromResult(_scenes.TryGetValue(FileKey(name), out var scene) ? scene : null);
        }
    }

    public async Task<StoreResult> SaveSceneAsync(AudioScene scene, bool overwrite)
    {
        var requested = FileKey(scene.Name);
        var precondition = overwrite ? WritePrecondition.Overwrite : WritePrecondition.MustNotExist;
        return await MutateAsync(async vault =>
        {
            // Sin distinguir mayúsculas: aunque el sistema de ficheros las distinga, «a» y «A» son la misma escena.
            var existing = Snapshot().Scenes.TryGetValue(requested, out var found) ? found.Name : null;
            if (existing is not null && !overwrite) return StoreResult.AlreadyExists;
            var name = existing ?? requested;
            var saved = scene with { Name = name };
            var result = await vault.WriteAsync(ScenePath(name), AudioSceneSerializer.Write(saved), precondition);
            if (result.Status != WriteStatus.Written) return StoreResult.AlreadyExists;
            Publish(scenes: With(Snapshot().Scenes, name, saved));
            return StoreResult.Ok;
        });
    }

    public async Task<StoreResult> RenameSceneAsync(string oldName, string newName)
    {
        var from = FileKey(oldName);
        var to = FileKey(newName);
        return await MutateAsync(async vault =>
        {
            var scenes = Snapshot().Scenes;
            if (!scenes.TryGetValue(from, out var scene)) return StoreResult.NotFound;
            from = scene.Name;
            if (from == to) return StoreResult.Ok;
            var onlyCase = string.Equals(from, to, StringComparison.OrdinalIgnoreCase);
            if (!onlyCase && scenes.ContainsKey(to)) return StoreResult.AlreadyExists;
            if (!await vault.MoveAsync(ScenePath(from), ScenePath(to))) return StoreResult.AlreadyExists;
            Publish(scenes: With(Without(scenes, from), to, scene with { Name = to }));
            return StoreResult.Ok;
        });
    }

    public async Task<StoreResult> DeleteSceneAsync(string name)
    {
        var key = FileKey(name);
        return await MutateAsync(async vault =>
        {
            var scenes = Snapshot().Scenes;
            if (!scenes.TryGetValue(key, out var scene)) return StoreResult.NotFound;
            await vault.DeleteAsync(ScenePath(scene.Name));
            Publish(scenes: Without(scenes, key));
            return StoreResult.Ok;
        });
    }

    public async Task<StoreResult> ToggleFavoriteAsync(string track)
    {
        var path = VaultPaths.Normalize(track).Trim('/');
        if (path.Length == 0) return StoreResult.NotFound;
        return await MutateAsync(async vault =>
        {
            lock (_gate)
            {
                if (_favoritesUnreadable)
                {
                    _logger.LogWarning("No se guardan los favoritos: {Path} no se pudo leer", FavoritesPath);
                    return StoreResult.Failed;
                }
            }
            var current = Snapshot().Favorites;
            var next = current.Contains(path, StringComparer.OrdinalIgnoreCase)
                ? [.. current.Where(item => !string.Equals(item, path, StringComparison.OrdinalIgnoreCase))]
                : (IReadOnlyList<string>)[.. current, path];
            var json = new JsonObject { ["version"] = 1, ["tracks"] = new JsonArray([.. next.Select(item => JsonValue.Create(item))]) };
            await vault.WriteAsync(FavoritesPath, json.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), WritePrecondition.Overwrite);
            Publish(favorites: next, favoritesUnreadable: false);
            return StoreResult.Ok;
        });
    }

    public int CountNotesCiting(string sceneName)
    {
        if (_host.Current is not { } vault) return 0;
        var names = SceneNames;
        return vault.Index.Entities.Count(entity =>
            FrontmatterReader.GetString(entity.Frontmatter, "ambience") is { } value
            && AmbienceResolver.Resolve(value, names, AudioLibrary.Empty) is { Kind: AmbienceKind.Scene } target
            && string.Equals(FileKey(target.Reference ?? ""), FileKey(sceneName), StringComparison.OrdinalIgnoreCase));
    }

    // Una sola escritura a la vez; un fallo de disco es un resultado, no una excepción.
    private async Task<StoreResult> MutateAsync(Func<VaultService, Task<StoreResult>> action)
    {
        if (_host.Current is not { } vault) return StoreResult.Failed;
        await _writeGate.WaitAsync();
        try
        {
            if (!ReferenceEquals(vault, _host.Current)) return StoreResult.Failed;
            return await action(vault);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ObjectDisposedException)
        {
            _logger.LogWarning("No se pudo escribir en {Folder} ({ExceptionType})", AudioPaths.AppFolder, ex.GetType().Name);
            return StoreResult.Failed;
        }
        finally
        {
            _writeGate.Release();
        }
    }

    private static string FileKey(string name) => NoteFileName.FromTitle(name);

    private static string ScenePath(string fileKey) => $"{ScenesFolder}/{fileKey}.json";

    private static Dictionary<string, AudioScene> With(IReadOnlyDictionary<string, AudioScene> source, string key, AudioScene scene) =>
        new(source, StringComparer.OrdinalIgnoreCase) { [key] = scene };

    private static Dictionary<string, AudioScene> Without(IReadOnlyDictionary<string, AudioScene> source, string key)
    {
        var copy = new Dictionary<string, AudioScene>(source, StringComparer.OrdinalIgnoreCase);
        copy.Remove(key);
        return copy;
    }

    private (IReadOnlyDictionary<string, AudioScene> Scenes, IReadOnlyList<string> Favorites) Snapshot()
    {
        lock (_gate) return (_scenes, _favorites);
    }

    private void Publish(IReadOnlyDictionary<string, AudioScene>? scenes = null, IReadOnlyList<string>? favorites = null, bool? favoritesUnreadable = null)
    {
        lock (_gate)
        {
            // Una recarga anterior que aún esté leyendo no debe pisar esta mutación.
            _generation++;
            if (scenes is not null)
            {
                _scenes = scenes;
                _sceneNames = [.. scenes.Values.Select(scene => scene.Name).Order(StringComparer.OrdinalIgnoreCase)];
            }
            if (favorites is not null) _favorites = favorites;
            if (favoritesUnreadable is { } unreadable) _favoritesUnreadable = unreadable;
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
