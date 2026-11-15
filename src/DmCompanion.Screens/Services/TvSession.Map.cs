using System.Collections.Immutable;
using DmCompanion.Core.Maps;
using DmCompanion.Core.Tv;
using DmCompanion.Core.Vault;

namespace DmCompanion.Screens.Services;

// El mapa en la TV: encuadre fijado desde la tablet y estado en memoria del mapa, publicado en cada cambio.
public sealed partial class TvSession
{
    private const double DefaultAspect = 16.0 / 9.0;
    private static readonly IReadOnlyDictionary<string, TvFile> NoFiles = new Dictionary<string, TvFile>();

    // Zona visible de la tablet al fijar, por mapa (sin ajustar: el ajuste a la proporción vigente se hace al usarla).
    // Inmutable para que la UI la lea sin el gate.
    private ImmutableDictionary<string, TvFrame> _frames = ImmutableDictionary.Create<string, TvFrame>(StringComparer.Ordinal);
    // Mapa de la TV: definición y estado en memoria más recientes, aunque la tablet tenga abierto otro mapa.
    private MapDefinition? _tvMap;
    private MapState? _tvState;
    private VaultService? _tvVault;

    /// <summary>Nota del mapa que muestra (o, en modo Content, mostraba) la TV.</summary>
    public string? MapPath => _tvMap?.NotePath;

    public string? MapName => _tvMap?.Name;

    /// <summary>Último mensaje `map`, también en modo Content (para «Volver al mapa»).</summary>
    public TvMapMessage? LastMap { get; private set; }

    private double Aspect => Viewport is { Width: > 0, Height: > 0 } viewport ? (double)viewport.Width / viewport.Height : DefaultAspect;

    /// <summary>Encuadre recordado de ese mapa, ajustado a la proporción de la TV (16:9 sin TV); null si no se fijó.</summary>
    public TvFrame? FrameFor(string mapPath) =>
        _frames.TryGetValue(mapPath, out var visible) ? visible.FitToAspect(Aspect) : null;

    /// <summary>Encuadre que la tablet marca sobre ese mapa: solo si la TV muestra (o vuelve a) ese mapa.</summary>
    public TvFrame? VisibleFrameFor(string? mapPath) =>
        mapPath is not null && Mode is TvMode.Map or TvMode.Content && MapPath == mapPath ? FrameFor(mapPath) : null;

    /// <summary>
    /// Fija como encuadre la zona del mapa abierto que se ve en la tablet y pasa la TV a ese mapa.
    /// No hace nada con la TV apagada, si ese mapa no es el abierto o si la zona no es válida.
    /// </summary>
    public async Task FixFrameAsync(string mapPath, TvFrame visible)
    {
        if (!(double.IsFinite(visible.X) && double.IsFinite(visible.Y) && double.IsFinite(visible.Width) && double.IsFinite(visible.Height))
            || visible.Width <= 0 || visible.Height <= 0) return;
        await _content.PrepareAsync();
        await _gate.WaitAsync();
        try
        {
            if (Mode == TvMode.Off || _maps.Current is not { } map || map.NotePath != mapPath || _maps.Editor is not { } editor) return;
            _frames = _frames.SetItem(mapPath, visible);
            (_tvMap, _tvState, _tvVault) = (map, editor.State, _host.Current);
            // Fijar un encuadre también saca a la TV del contenido que mostraba.
            _showing = null;
            Mode = TvMode.Map;
            RefreshMap();
        }
        finally
        {
            _gate.Release();
        }
        Notify();
    }

    // Llamar con _gate tomado. Proyecta el mapa de la TV con su encuadre; solo se envía en modo Map. El id de cada imagen sale
    // de su fecha en la lista de imágenes: la cargan FixFrameAsync y los cambios de vault y la recargan los de imágenes
    // (OnImagesChanged) antes de tomar el gate; mientras tanto se usa la anterior.
    private void RefreshMap()
    {
        if (_tvMap is not { } map || _tvState is not { } state || FrameFor(map.NotePath) is not { } frame) return;
        LastMap = _feed.Build(map, state, frame);
        if (Mode == TvMode.Map) Send(LastMap.Json, LastMap.Files);
    }

    // El mapa de la TV abierto en la tablet cambió (token, trazo, combate, deshacer…): se republica desde memoria.
    private void OnMapChanged(object? sender, EventArgs e) => _ = UnderGateAsync(() =>
    {
        if (Mode is not (TvMode.Map or TvMode.Content) || _tvMap is not { } tv) return false;
        if (_maps.Current is not { } map || map.NotePath != tv.NotePath || _maps.Editor is not { } editor) return false;
        if (ReferenceEquals(editor.State, _tvState) && map == tv) return false;
        (_tvMap, _tvState) = (map, editor.State);
        RefreshMap();
        return false;
    }, "No se pudo mandar el mapa a la TV ({ExceptionType}).");

    // Llamar con _gate tomado, tras un cambio del vault con el mapa de la TV aún en él. Si la tablet no lo tiene abierto, su
    // definición (imagen) se relee del índice; abierto, manda la de MapSession. Se proyecta igual en los dos casos: los
    // retratos y la vida máxima de los tokens salen de sus notas.
    private void RefreshMapFromVault()
    {
        if (_tvMap is not { } tv) return;
        if (_maps.Current?.NotePath != tv.NotePath && _host.Current?.Index.Get(tv.NotePath) is { } entity
            && MapDefinition.FromEntity(entity) is { } fresh) _tvMap = fresh;
        RefreshMap();
    }

    // Llamar con _gate tomado.
    private bool TvMapIsGone() =>
        _tvMap is { } tv && (_host.Current is not { } vault || !ReferenceEquals(vault, _tvVault)
            || vault.Index.Get(tv.NotePath) is not { } entity || MapDefinition.FromEntity(entity) is null);

    // Llamar con _gate tomado. El mapa de la TV ya no existe: se olvida y, si se estaba viendo, la TV vuelve a la espera.
    private void LeaveMap()
    {
        if (_tvMap is { } tv) _frames = _frames.Remove(tv.NotePath);
        (_tvMap, _tvState, _tvVault, LastMap) = (null, null, null, null);
        if (Mode != TvMode.Map) return;
        Mode = TvMode.Waiting;
        PublishWaiting();
    }

    // Llamar con _gate tomado, al desactivar: los encuadres y los ids de ficheros solo viven mientras la TV está activada.
    private void ForgetMaps()
    {
        _frames = _frames.Clear();
        (_tvMap, _tvState, _tvVault, LastMap, _lastSent) = (null, null, null, null, null);
        _feed.Reset();
    }
}
