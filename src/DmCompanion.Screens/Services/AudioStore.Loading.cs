using System.Text.Json;
using System.Text.Json.Nodes;
using DmCompanion.Core.Audio;
using DmCompanion.Core.Vault;
using DmCompanion.Core.Vault.Storage;
using Microsoft.Extensions.Logging;

namespace DmCompanion.Screens.Services;

// Lectura de escenas y favoritos al abrir el vault y cuando cambian sus ficheros.
public sealed partial class AudioStore
{
    private void OnHostChanged(object? sender, EventArgs e) => Attach();

    private void Attach()
    {
        var vault = _host.Current;
        lock (_gate)
        {
            if (ReferenceEquals(vault, _vault)) return;
            if (_vault is { } previous) previous.FileChanged -= OnFileChanged;
            _vault = vault;
            if (vault is not null) vault.FileChanged += OnFileChanged;
        }
        if (vault is null)
        {
            Interlocked.Increment(ref _generation);
            Publish(new Dictionary<string, AudioScene>(), [], favoritesUnreadable: false);
        }
        else _ = ReloadAsync(vault);
    }

    private void OnFileChanged(object? sender, VaultChange change)
    {
        if (sender is not VaultService vault) return;
        if (change.Kind != VaultChangeKind.Rescan
            && !VaultPaths.Normalize(change.RelativePath).StartsWith(AppPrefix, StringComparison.OrdinalIgnoreCase))
            return;
        _ = ReloadAsync(vault);
    }

    private async Task ReloadAsync(VaultService vault)
    {
        var generation = Interlocked.Increment(ref _generation);
        try
        {
            var files = await vault.ListFilesAsync(IsStoreFile);
            var scenes = new Dictionary<string, AudioScene>(StringComparer.OrdinalIgnoreCase);
            IReadOnlyList<string> favorites = [];
            var favoritesUnreadable = false;
            foreach (var file in files.OrderBy(file => file.RelativePath, StringComparer.OrdinalIgnoreCase))
            {
                if (VaultPaths.IsConflictFile(file.RelativePath))
                {
                    _logger.LogWarning("Se ignora la copia en conflicto {Path}", file.RelativePath);
                    continue;
                }
                string content;
                try
                {
                    content = (await vault.ReadRawAsync(file.RelativePath)).Content;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    _logger.LogWarning("No se pudo leer {Path}; se salta ({ExceptionType})", file.RelativePath, ex.GetType().Name);
                    favoritesUnreadable |= IsFavoritesFile(file.RelativePath);
                    continue;
                }
                if (IsFavoritesFile(file.RelativePath))
                {
                    var read = ReadFavorites(content, file.RelativePath);
                    (favorites, favoritesUnreadable) = (read ?? [], read is null);
                }
                else if (ReadScene(content, file.RelativePath) is { } scene) scenes[scene.Name] = scene;
            }
            lock (_gate)
            {
                if (generation != _generation || !ReferenceEquals(vault, _vault)) return;
            }
            Publish(scenes, favorites, favoritesUnreadable);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ObjectDisposedException)
        {
            _logger.LogWarning("No se pudieron leer las escenas de audio ({ExceptionType})", ex.GetType().Name);
        }
    }

    private static bool IsStoreFile(string rel)
    {
        var path = VaultPaths.Normalize(rel);
        if (!path.StartsWith(AppPrefix, StringComparison.OrdinalIgnoreCase) || !path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) return false;
        return IsFavoritesFile(path)
            || (VaultPaths.IsConflictFile(path) && string.Equals(VaultPaths.GetConflictOriginal(path), FavoritesPath, StringComparison.OrdinalIgnoreCase))
            || (path.StartsWith(ScenesFolder + "/", StringComparison.OrdinalIgnoreCase) && path.IndexOf('/', ScenesFolder.Length + 1) < 0);
    }

    private AudioScene? ReadScene(string content, string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        var (scene, repairs) = AudioSceneSerializer.Read(content, name);
        if (scene is null) _logger.LogWarning("Escena inválida, se ignora: {Path}", path);
        else if (repairs > 0) _logger.LogWarning("Escena reparada ({Repairs} arreglos): {Path}", repairs, path);
        return scene;
    }

    private static bool IsFavoritesFile(string path) => string.Equals(path, FavoritesPath, StringComparison.OrdinalIgnoreCase);

    // null si el fichero no es válido.
    private IReadOnlyList<string>? ReadFavorites(string content, string path)
    {
        try
        {
            if (JsonNode.Parse(content) is JsonObject { } root && root["tracks"] is JsonArray tracks)
            {
                return [.. tracks
                    .Select(node => node is JsonValue value && value.TryGetValue<string>(out var text) ? text.Trim() : null)
                    .OfType<string>()
                    .Where(text => text.Length > 0)
                    .Distinct(StringComparer.OrdinalIgnoreCase)];
            }
        }
        catch (JsonException)
        {
        }
        _logger.LogWarning("Favoritos inválidos, se ignoran: {Path}", path);
        return null;
    }
}
