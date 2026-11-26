using System.Text.Json;
using DmCompanion.Core.Tv;

namespace DmCompanion.Screens.Services;

/// <summary>
/// «Vista de jugadores» de la tablet: el mapa abierto tal como lo ven los jugadores (mapa entero, solo para mirar).
/// Scoped, como el lienzo. Se desactiva al cerrarse el mapa; al abrir otro sigue activa.
/// Quien llame a <see cref="BuildJson"/> espera antes a <see cref="NoteImageResolver.EnsureLoadedAsync"/>: sin la lista de
/// imágenes cargada no hay URLs.
/// </summary>
public sealed class PlayerPreview : IDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly MapSession _session;
    private readonly VaultHost _host;
    private readonly TokenHitPoints _hitPoints;
    private readonly NoteImageResolver _images;

    public PlayerPreview(MapSession session, VaultHost host, TokenHitPoints hitPoints, NoteImageResolver images)
    {
        (_session, _host, _hitPoints, _images) = (session, host, hitPoints, images);
        _session.Changed += OnSessionChanged;
    }

    public bool IsActive { get; private set; }

    /// <summary>Cambió <see cref="IsActive"/>. Puede llegar desde otros hilos (cierre del mapa por el watcher).</summary>
    public event EventHandler? Changed;

    public void Toggle()
    {
        IsActive = !IsActive;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Objeto `map` de la vista de jugador del mapa abierto (encuadre = mapa entero); null sin mapa abierto.</summary>
    public string? BuildJson()
    {
        if (_session.Current is not { } map || _session.Editor is not { } editor) return null;
        var tabletUrls = _images.TabletUrls();
        var view = PlayerViewProjector.Project(map, editor.State, new TvFrame(0, 0, map.Width, map.Height),
            _hitPoints.Rules, _hitPoints.MaxFor, t => TvMapFeed.LookOf(_host, t));
        return JsonSerializer.Serialize(PlayerMapJson.Map(view, p => p is null ? null : tabletUrls(p), wholeMap: true), Json);
    }

    public void Dispose() => _session.Changed -= OnSessionChanged;

    private void OnSessionChanged(object? sender, EventArgs e)
    {
        if (!IsActive || _session.Current is not null) return;
        IsActive = false;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
