using System.Text.Json;
using DmCompanion.Core.Glossary;
using DmCompanion.Core.Maps;
using DmCompanion.Core.Tv;
using DmCompanion.Core.Vault;

namespace DmCompanion.Screens.Services;

/// <summary>Mensaje `map` para la TV y la lista blanca de ficheros que cita (id → fichero).</summary>
public sealed record TvMapMessage(string Json, IReadOnlyDictionary<string, TvFile> Files);

/// <summary>
/// Traduce la vista de jugador (<see cref="PlayerViewProjector"/>) al mensaje `map` de la TV. Las rutas del vault nunca
/// salen: cada imagen se cita como `files/{id}`, con un id opaco (contador). El id es estable mientras no cambien el vault ni
/// la fecha de modificación del fichero (según <see cref="NoteImageResolver"/>), y no se llame a <see cref="Reset"/>; si cambian,
/// el fichero recibe un id nuevo y la TV, que guarda las imágenes por URL, lo vuelve a pedir.
/// </summary>
public sealed class TvMapFeed(VaultHost host, TokenHitPoints hitPoints, NoteImageResolver images)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly Lock _lock = new();
    private readonly Dictionary<(string Path, long Stamp), string> _ids = [];
    // Vault de los ids de _ids: al cambiar de vault se olvidan y una misma ruta recibe un id nuevo.
    private VaultService? _idsVault;
    // Nunca vuelve a empezar: la página de la TV puede seguir abierta (con su caché) tras desactivar y reactivar.
    private int _nextId;

    public TvMapMessage Build(MapDefinition map, MapState state, TvFrame frame)
    {
        var view = PlayerViewProjector.Project(map, state, frame, hitPoints.Rules, hitPoints.MaxFor, t => LookOf(host, t));
        var files = new Dictionary<string, TvFile>(StringComparer.Ordinal);
        var message = new { kind = "map", map = PlayerMapJson.Map(view, p => UrlFor(p, files), wholeMap: false) };
        return new TvMapMessage(JsonSerializer.Serialize(message, Json), files);
    }

    /// <summary>Olvida los ids (la TV se desactiva). Los nuevos siguen la numeración: un id nunca se reutiliza para otro fichero.</summary>
    public void Reset()
    {
        lock (_lock)
        {
            _ids.Clear();
            _idsVault = null;
        }
    }

    /// <summary>Tipo de contenido de una imagen que la TV puede pedir, según la extensión; null si no es una imagen admitida.</summary>
    public static string? ContentTypeFor(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".webp" => "image/webp",
        ".gif" => "image/gif",
        _ => null,
    };

    // Un PJ con alias conserva el retrato en la TV: el alias oculta la identidad, no la cara del personaje.
    internal static TokenLook LookOf(VaultHost host, MapToken token)
    {
        if (token.Source is not { } source || host.Current is not { } vault) return new TokenLook(null, false);
        var note = TokenSourceResolver.Resolve(source, vault.Index, _ => null).Note;
        var portrait = note?.Portrait is { } p && !string.IsNullOrWhiteSpace(p) ? VaultPaths.Normalize(p) : null;
        return new TokenLook(portrait, note?.Type == EntityType.Pc);
    }

    /// <summary>
    /// Da de alta una imagen del vault en la lista blanca <paramref name="files"/> y devuelve su URL (`files/{id}`); null si la
    /// ruta no es una imagen segura del vault. Los ids son los mismos para el mapa y el contenido: la TV nunca ve un id reutilizado
    /// para otro fichero (su caché de imágenes no se confunde).
    /// </summary>
    internal string? UrlFor(string? path, Dictionary<string, TvFile> files)
    {
        // Una ruta que sale del vault (`..`, absoluta) no entra nunca en la lista blanca.
        if (!VaultPaths.TryNormalizeSafe(path, out var safe) || ContentTypeFor(safe) is not { } contentType) return null;
        if (host.Current is not { } vault) return null;
        path = safe;
        // Sin la lista de imágenes cargada no hay fecha (0): el id solo depende de la ruta, como antes de cargarla.
        var key = (path, images.LastWriteOf(path)?.Ticks ?? 0);
        string id;
        lock (_lock)
        {
            if (!ReferenceEquals(_idsVault, vault))
            {
                _ids.Clear();
                _idsVault = vault;
            }
            if (!_ids.TryGetValue(key, out id!)) _ids[key] = id = (++_nextId).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        files[id] = new TvFile(contentType, ct => OpenAsync(vault, path, ct));
        return $"files/{id}";
    }

    // Cada fichero va atado al vault para el que se dio de alta y solo se sirve mientras ese vault siga abierto: al cambiar de
    // vault, VaultHost cambia Current antes de avisar, y en ese hueco la lista blanca vieja no debe abrir rutas del vault nuevo.
    private Task<Stream> OpenAsync(VaultService vault, string path, CancellationToken ct) =>
        ReferenceEquals(host.Current, vault) ? vault.OpenReadAsync(path, ct) : throw new FileNotFoundException("El vault ya no está abierto.");
}
