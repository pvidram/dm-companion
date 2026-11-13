using DmCompanion.Core.GameSystems;
using DmCompanion.Core.Maps;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace DmCompanion.Screens.Services;

public enum MapMode { Navigate, Draw, Measure }

public sealed record DrawTool(string Color, double Width, bool Eraser)
{
    /// <summary>Tinta del design system (`--ink-900`); es también el primer color de la paleta de la barra.</summary>
    public const string DefaultColor = "#2B2118";

    public static DrawTool Default { get; } = new(DefaultColor, 6, false);
}

/// <summary>Posición en pantalla (viewport, en px CSS) del centro del token tocado.</summary>
public sealed record TokenTappedEventArgs(string Id, double ScreenX, double ScreenY);

/// <summary>Área del lienzo en el viewport (px CSS).</summary>
public sealed record MapAreaRect(double Left, double Top, double Width, double Height);

/// <summary>
/// Único punto de interop del lienzo del mapa (`window.mapCanvas`): los componentes nunca inyectan IJSRuntime.
/// Scoped: hay un lienzo por circuito.
/// </summary>
public sealed class MapCanvasInterop(
    IJSRuntime js,
    MapSession session,
    VaultFileUrls urls,
    VaultHost host,
    PortraitUrlProvider portraits,
    ILogger<MapCanvasInterop> logger) : IAsyncDisposable
{
    /// <summary>Lado mayor máximo (px) que el WebView de la tablet pinta sin fallar; ver la prueba de la Task 0.</summary>
    public const int MaxImageSide = 8192;

    private const double MinStrokeWidth = 1;
    private const double MaxStrokeWidth = 100;
    private static readonly Regex StrokeColor = new("^#[0-9a-fA-F]{6}\\z", RegexOptions.CultureInvariant);

    // Serializa carga, repintado y descarga: un repintado nunca se adelanta a la carga que lo precede.
    private readonly SemaphoreSlim _gate = new(1, 1);
    private DotNetObjectReference<MapCanvasInterop>? _self;
    private bool _loaded;
    // Estado y modo que se enviaron la última vez: un guardado o un aviso no cambian el State y no repintan.
    private MapState? _paintedState;
    private bool _paintedReadOnly;

    /// <summary>Lo consume la tarjeta rápida del token.</summary>
    public event EventHandler<TokenTappedEventArgs>? TokenTapped;

    public async Task LoadAsync(ElementReference hostElement)
    {
        await _gate.WaitAsync();
        try
        {
            if (await BuildDtoAsync() is not { } built) return;
            _self ??= DotNetObjectReference.Create(this);
            _loaded = await TryInvokeAsync("mapCanvas.load", hostElement, built.Dto, _self);
            if (!_loaded) return;
            // Se recuerda lo que se envió, no lo vigente: un Mutate durante BuildDtoAsync debe repintarse después.
            (_paintedState, _paintedReadOnly) = (built.State, built.ReadOnly);
            // Un lienzo nuevo nace en Navegar: si la barra ya tenía otro modo, se le vuelve a decir.
            if (Mode != MapMode.Navigate) await TryInvokeAsync("mapCanvas.setMode", Mode.ToString(), Tool);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Repintado completo con el estado vigente. No hace nada si el lienzo aún no está cargado.</summary>
    public async Task RenderAsync()
    {
        await _gate.WaitAsync();
        try
        {
            if (!_loaded || session.Editor?.State is not { } state) return;
            if (ReferenceEquals(state, _paintedState) && session.IsReadOnly == _paintedReadOnly) return;
            if (await BuildDtoAsync() is not { } built) return;
            (_paintedState, _paintedReadOnly) = (built.State, built.ReadOnly);
            await TryInvokeAsync("mapCanvas.render", built.Dto);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Modo y herramienta elegidos en la barra. Se recuerdan aquí para sobrevivir a la recarga del lienzo y de la barra.</summary>
    public MapMode Mode { get; private set; } = MapMode.Navigate;
    public DrawTool Tool { get; private set; } = DrawTool.Default;

    public Task SetModeAsync(MapMode mode, DrawTool tool)
    {
        (Mode, Tool) = (mode, tool);
        return InvokeLoadedAsync("mapCanvas.setMode", mode.ToString(), tool);
    }

    public Task FitAsync() => InvokeLoadedAsync("mapCanvas.fit");

    /// <summary>Centro de la vista en coordenadas del mapa; null si no hay lienzo cargado o la respuesta no es válida.</summary>
    public async Task<(double X, double Y)?> ViewCenterAsync() =>
        await QueryLoadedAsync("mapCanvas.viewCenter", 2) is [var x, var y] ? (x, y) : null;

    /// <summary>Área del lienzo en el viewport; null si no hay lienzo cargado o la respuesta no es válida.</summary>
    public async Task<MapAreaRect?> HostRectAsync() =>
        await QueryLoadedAsync("mapCanvas.hostRect", 4) is [var left, var top, var width and > 0, var height and > 0]
            ? new MapAreaRect(left, top, width, height)
            : null;

    /// <summary>Libera el lienzo JS (el elemento desaparece o el mapa se cierra).</summary>
    public async Task UnloadAsync()
    {
        await _gate.WaitAsync();
        try
        {
            if (!_loaded) return;
            _loaded = false;
            await TryInvokeAsync("mapCanvas.dispose");
        }
        finally
        {
            _gate.Release();
        }
    }

    [JSInvokable]
    public void OnImageLoaded(string mapPath, double width, double height)
    {
        if (!double.IsFinite(width) || !double.IsFinite(height) || width <= 0 || height <= 0) return;
        session.SetImageSize(mapPath, width, height);
    }

    // El JS informa con la nota que cargó: un evento tardío de otro mapa se descarta.
    [JSInvokable]
    public void OnImageFailed(string mapPath)
    {
        if (session.Current?.NotePath == mapPath) session.ReportImageMissing();
    }

    [JSInvokable]
    public void OnTokenMoved(string id, double x, double y)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y)) return;
        session.Mutate(editor =>
        {
            if (editor.State.Tokens.FirstOrDefault(t => t.Id == id) is not { } token) return false;
            var (snappedX, snappedY) = GridMath.SnapCenter(editor.State.Grid, token.Size, x, y);
            return editor.MoveToken(id, snappedX, snappedY);
        });
    }

    // Los gestos solo llegan con el lápiz en modo Dibujar, pero lo que viene del JS se valida igual: va al `.state.json`.
    // Devuelve si el trazo se aceptó; si no, el lienzo retira el trazo provisional que pintó.
    [JSInvokable]
    public bool OnStrokeAdded(string color, double width, double[] points)
    {
        if (color is null || !StrokeColor.IsMatch(color) || !double.IsFinite(width) || width < MinStrokeWidth || width > MaxStrokeWidth) return false;
        if (points is not { Length: >= 6 } || points.Length % 3 != 0 || !points.All(double.IsFinite)) return false;
        var accepted = false;
        session.Mutate(editor => accepted = editor.AddStroke(color, width, points) is not null);
        return accepted;
    }

    [JSInvokable]
    public void OnStrokeErased(string id) => session.Mutate(editor => editor.EraseStroke(id));

    [JSInvokable]
    public void OnTokenTapped(string id, double screenX, double screenY) =>
        TokenTapped?.Invoke(this, new TokenTappedEventArgs(id, screenX, screenY));

    public async ValueTask DisposeAsync()
    {
        await UnloadAsync();
        _self?.Dispose();
    }

    private async Task InvokeLoadedAsync(string identifier, params object?[] args)
    {
        await _gate.WaitAsync();
        try
        {
            if (_loaded) await TryInvokeAsync(identifier, args);
        }
        finally
        {
            _gate.Release();
        }
    }

    // Consulta de lectura al lienzo: solo se acepta un array finito de la longitud esperada.
    private async Task<double[]?> QueryLoadedAsync(string identifier, int length)
    {
        await _gate.WaitAsync();
        try
        {
            if (!_loaded) return null;
            var answer = await js.InvokeAsync<double[]?>(identifier);
            return answer is { } values && values.Length == length && values.All(double.IsFinite) ? values : null;
        }
        catch (JSDisconnectedException)
        {
            return null; // El circuito ya se cerró.
        }
        catch (Exception ex) when (ex is JSException or TaskCanceledException)
        {
            logger.LogWarning("Falló la consulta {Identifier} al lienzo del mapa ({ExceptionType}).", identifier, ex.GetType().FullName);
            return null;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<bool> TryInvokeAsync(string identifier, params object?[] args)
    {
        try
        {
            await js.InvokeVoidAsync(identifier, args);
            return true;
        }
        catch (JSDisconnectedException)
        {
            return false; // El circuito ya se cerró.
        }
        catch (Exception ex) when (ex is JSException or TaskCanceledException)
        {
            // Sin `ex`: el mensaje de JS podría citar contenido del mapa.
            logger.LogWarning("Falló la llamada {Identifier} al lienzo del mapa ({ExceptionType}).", identifier, ex.GetType().FullName);
            return false;
        }
    }

    // Foto única del estado al empezar: los awaits de abajo no pueden mezclar dos estados en un mismo DTO.
    private async Task<(object Dto, MapState State, bool ReadOnly)?> BuildDtoAsync()
    {
        if (session.Current is not { } map || session.Editor is not { } editor) return null;
        var state = editor.State;
        var readOnly = session.IsReadOnly;
        var vault = host.Current;
        // Regla de medición del sistema de juego activo (con Chebyshev: máx(Δcasillas) × cantidad).
        var unit = (vault?.ActiveSystem.Grid ?? GridRules.Feet5).DefaultUnit;

        var tokens = new List<object>(state.Tokens.Count);
        foreach (var token in state.Tokens)
        {
            var entity = token.Source is { } source ? vault?.Index.Get(source) : null;
            var portrait = await portraits.GetAsync(entity?.Portrait);
            tokens.Add(new
            {
                id = token.Id,
                name = token.Name,
                initial = entity?.Initial ?? InitialOf(token.Name),
                portraitUrl = portrait.Url,
                faction = TokenSideMapping.ToFaction(token.Side).ToString().ToLowerInvariant(),
                size = token.Size,
                x = token.X,
                y = token.Y,
            });
        }

        var dto = new
        {
            mapPath = map.NotePath,
            imageUrl = await ImageUrlAsync(map),
            width = map.Width,
            height = map.Height,
            grid = state.Grid,
            unitAmount = unit.Amount,
            unitLabel = unit.Name,
            tokens,
            strokes = state.Strokes,
            readOnly,
            maxImageSide = MaxImageSide,
        };
        return (dto, state, readOnly);
    }

    // Si la imagen no existe se envía igualmente una URL (con mtime 0): el 404 llega al `onerror` del lienzo,
    // que avisa con OnImageFailed y pinta el lienzo por defecto. Un único camino para todos los fallos de carga.
    private async Task<string?> ImageUrlAsync(MapDefinition map)
    {
        if (map.ImagePath is not { } path) return null;
        DateTime lastWrite = DateTime.UnixEpoch;
        try
        {
            if (host.Current is { } vault && await vault.StatAsync(path) is { } info) lastWrite = info.LastWriteTimeUtc;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning("No se pudo leer la fecha de la imagen del mapa ({ExceptionType}).", ex.GetType().FullName);
        }
        var url = urls.UrlFor(path, lastWrite);
        // Una ruta insegura no llega al JS como URL, así que no habría onerror: se avisa aquí, igual que con una imagen que falta.
        if (url is null) session.ReportImageMissing();
        return url;
    }

    private static string InitialOf(string name) =>
        name.Length > 0 ? char.ToUpperInvariant(name[0]).ToString() : "?";
}
