using System.Text.Json;
using DmCompanion.Core.Tv;
using DmCompanion.Screens.Platform;
using Microsoft.Extensions.Logging;

namespace DmCompanion.Screens.Services;

/// <summary>
/// Estado de la TV para toda la app: si está activada, con qué dirección, cuántas TVs hay y qué se les manda.
/// Es lo único que publica hacia <see cref="ITvServer"/>.
/// </summary>
public sealed partial class TvSession
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly ITvServer _server;
    private readonly VaultHost _host;
    private readonly MapSession _maps;
    private readonly TvMapFeed _feed;
    private readonly TvContentFeed _content;
    private readonly IAppPreferences _prefs;
    private readonly ILogger<TvSession> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private string? _publishedCampaign;
    private string? _lastSent;

    public TvSession(ITvServer server, VaultHost host, MapSession maps, TvMapFeed feed, TvContentFeed content, IAppPreferences prefs, ILogger<TvSession> logger)
    {
        _server = server;
        _host = host;
        _maps = maps;
        _feed = feed;
        _content = content;
        _prefs = prefs;
        _logger = logger;
        _server.ConnectionsChanged += OnConnectionsChanged;
        _server.ViewportReported += OnViewportReported;
        _host.Changed += OnVaultChanged;
        _maps.Changed += OnMapChanged;
        _content.ImagesChanged += OnImagesChanged;
    }

    public TvMode Mode { get; private set; }

    public TvEndpoint? Endpoint { get; private set; }

    public int Connected { get; private set; }

    /// <summary>Último tamaño de ventana informado por una TV.</summary>
    public TvViewport? Viewport { get; private set; }

    /// <summary>Puede llegar desde cualquier hilo (conexiones de la TV, cambios del vault).</summary>
    public event EventHandler? Changed;

    /// <summary>Activa la TV en espera. Si ya lo está, no hace nada. Usa el código guardado (y lo guarda si era una propuesta). Propaga <see cref="IOException"/> si no hay puerto libre.</summary>
    public async Task EnableAsync()
    {
        await _gate.WaitAsync();
        try
        {
            if (Mode != TvMode.Off) return;
            var code = Code;
            Endpoint = await _server.StartAsync(code);
            _prefs.Set(CodeKey, code);
            Mode = TvMode.Waiting;
            PublishWaiting();
        }
        finally
        {
            _gate.Release();
        }
        Notify();
    }

    public async Task DisableAsync()
    {
        await _gate.WaitAsync();
        try
        {
            if (Mode == TvMode.Off) return;
            await _server.StopAsync();
            Mode = TvMode.Off;
            Endpoint = null;
            Connected = 0;
            Viewport = null;
            _showing = null;
            ForgetMaps();
        }
        finally
        {
            _gate.Release();
        }
        Notify();
    }

    /// <summary>
    /// La app se cierra (en Android, la actividad termina de verdad; pasar a segundo plano no cuenta): apaga la TV, que si no
    /// seguiría servida en la wifi mientras viva el proceso. Nunca lanza: llega desde el ciclo de vida de la plataforma.
    /// </summary>
    public async Task DisableOnExitAsync()
    {
        try
        {
            await DisableAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError("No se pudo apagar la TV al cerrar la app ({ExceptionType}).", ex.GetType().FullName);
        }
    }

    // Llamar con _gate tomado.
    private void PublishWaiting()
    {
        var campaign = CampaignName();
        Send(JsonSerializer.Serialize(new { kind = "waiting", campaign }, Json), NoFiles);
        _publishedCampaign = campaign;
    }

    // Llamar con _gate tomado. Todo lo que va a la TV pasa por aquí: un mensaje igual al último no se reenvía (p. ej. un
    // guardado que no cambia el mapa), y eso solo es correcto si _lastSent es de verdad lo último publicado.
    private void Send(string json, IReadOnlyDictionary<string, TvFile> files)
    {
        if (json == _lastSent) return;
        // La lista blanca antes que el mensaje: cuando la TV pide una imagen nueva, ya está permitida.
        _server.SetFiles(files);
        _server.Publish(json);
        _lastSent = json;
    }

    // Título de la nota de campaña o, si no hay, el nombre de la carpeta del vault.
    private string CampaignName()
    {
        if (_host.Current is not { } vault) return string.Empty;
        if (vault.Campaign.CampaignPath is { } path && vault.Index.Get(path) is { } note) return note.Title;
        return Path.GetFileName(vault.RootPath.TrimEnd('/', '\\'));
    }

    // VaultHost.Changed llega con cada edición de nota. Con un mapa, si ese mapa ya no existe (borrado, renombrado u otro
    // vault) la TV lo olvida; si sigue, se vuelve a proyectar (su nota o las de sus tokens pueden haber cambiado). Con un
    // contenido, se vuelve a generar o, si ya no se puede mostrar, la TV vuelve al mapa. En espera solo se vuelve a
    // publicar si cambia el nombre de la campaña. Lo que no cambia no se reenvía (Send descarta los repetidos).
    private void OnVaultChanged(object? sender, EventArgs e) => _ = UnderGateAsync(() =>
    {
        var gone = TvMapIsGone();
        if (gone) LeaveMap();
        else RefreshMapFromVault();
        var contentChanged = RefreshContent();
        if (Mode == TvMode.Waiting && CampaignName() != _publishedCampaign) PublishWaiting();
        return gone || contentChanged;
    }, "No se pudo avisar a la TV del cambio de vault ({ExceptionType}).",
        // La lista de imágenes del vault se recarga antes de tomar el gate: el contenido puede citar imágenes nuevas, y la
        // fecha de cada imagen decide su id.
        () => Mode != TvMode.Off ? _content.PrepareAsync() : Task.CompletedTask);

    // Ejecuta un cambio con el gate tomado (tras esperar a prepare, si lo hay) y avisa tras soltarlo si el cambio lo pide.
    // Nunca lanza: llega desde eventos.
    private async Task UnderGateAsync(Func<bool> change, string failure, Func<Task>? prepare = null)
    {
        bool notify;
        try
        {
            if (prepare is not null) await prepare();
            await _gate.WaitAsync();
            try
            {
                notify = change();
            }
            finally
            {
                _gate.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(failure, ex.GetType().FullName);
            return;
        }
        if (notify) Notify();
    }

    private void OnConnectionsChanged(object? sender, EventArgs e)
    {
        Connected = _server.ConnectedCount;
        Notify();
    }

    // Con otro tamaño de TV el encuadre se reajusta (mismo centro, sin recortar) y se vuelve a mandar.
    private void OnViewportReported(object? sender, TvViewport viewport) => _ = UnderGateAsync(() =>
    {
        Viewport = viewport;
        if (Mode != TvMode.Off) RefreshMap();
        return true;
    }, "No se pudo reajustar el encuadre de la TV ({ExceptionType}).");

    private void Notify() => Changed?.Invoke(this, EventArgs.Empty);
}
