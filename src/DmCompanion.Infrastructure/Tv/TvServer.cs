using System.Net;
using System.Net.Sockets;
using DmCompanion.Core.Tv;
using Microsoft.Extensions.Logging;

namespace DmCompanion.Infrastructure.Tv;

/// <summary>
/// Servidor HTTP mínimo sobre <see cref="TcpListener"/> (ASP.NET Core no existe en Android). Solo escucha entre
/// StartAsync y StopAsync y solo responde bajo <c>/tv/{código}/</c>. Cada conexión se atiende en su propia tarea.
/// </summary>
public sealed partial class TvServer : ITvServer
{
    public const int DefaultFirstPort = 8590;
    public const int DefaultPortCount = 10;

    private static readonly TimeSpan HeadTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan PingInterval = TimeSpan.FromSeconds(15);

    private readonly ITvAssets _assets;
    private readonly ILogger<TvServer> _logger;
    private readonly int _firstPort;
    private readonly int _portCount;
    private readonly Lock _gate = new();
    private readonly HashSet<TvEventStream> _streams = [];
    private readonly HashSet<Task> _connections = [];

    private TcpListener? _listener;
    private CancellationTokenSource? _stopping;
    private Task _acceptLoop = Task.CompletedTask;
    private string _code = string.Empty;
    private string? _last;
    private IReadOnlyDictionary<string, TvFile> _files = new Dictionary<string, TvFile>();

    public TvServer(ITvAssets assets, ILogger<TvServer> logger) : this(assets, logger, DefaultFirstPort, DefaultPortCount) { }

    internal TvServer(ITvAssets assets, ILogger<TvServer> logger, int firstPort, int portCount)
    {
        _assets = assets;
        _logger = logger;
        _firstPort = firstPort;
        _portCount = portCount;
    }

    public bool IsRunning { get { lock (_gate) return _listener is not null; } }

    public int ConnectedCount { get { lock (_gate) return _streams.Count; } }

    /// <summary>Plazo de cada escritura del canal de eventos: una TV que desaparece sin cerrar se da por caída.</summary>
    internal TimeSpan WriteTimeout { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>Conexiones simultáneas como máximo; las que sobran se cierran nada más aceptarlas.</summary>
    internal int MaxConnections { get; init; } = 64;

    public event EventHandler? ConnectionsChanged;

    public event EventHandler<TvViewport>? ViewportReported;

    public Task<TvEndpoint> StartAsync(string code, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        int port;
        lock (_gate)
        {
            if (_listener is not null) throw new InvalidOperationException("El servidor de la TV ya está activo.");
            var listener = Listen();
            _listener = listener;
            _code = code.Trim().ToLowerInvariant();
            _last = null;
            _stopping = new CancellationTokenSource();
            _acceptLoop = AcceptLoopAsync(listener, _stopping.Token);
            port = ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        _logger.LogInformation("Servidor de la TV activo en el puerto {Port}.", port);
        return Task.FromResult(new TvEndpoint(port, TvAddresses.Find()));
    }

    private TcpListener Listen()
    {
        for (var port = _firstPort; port < _firstPort + _portCount; port++)
        {
            var listener = new TcpListener(IPAddress.Any, port);
            try
            {
                listener.Start();
                return listener;
            }
            catch (SocketException ex) when (ex.SocketErrorCode is SocketError.AddressAlreadyInUse or SocketError.AccessDenied)
            {
                listener.Dispose();
            }
        }
        throw new IOException($"Puertos {_firstPort}–{_firstPort + _portCount - 1} ocupados.");
    }

    public async Task StopAsync()
    {
        Task[] pending;
        CancellationTokenSource stopping;
        bool hadStreams;
        lock (_gate)
        {
            if (_listener is null) return;
            _listener.Stop();
            _listener = null;
            stopping = _stopping!;
            _stopping = null;
            hadStreams = _streams.Count > 0;
            _streams.Clear();
            _last = null;
            _files = new Dictionary<string, TvFile>();
            pending = [_acceptLoop, .. _connections];
        }
        await stopping.CancelAsync();
        await Task.WhenAll(pending);
        stopping.Dispose();
        _logger.LogInformation("Servidor de la TV parado.");
        if (hadStreams) RaiseConnectionsChanged();
    }

    public void Publish(string json)
    {
        lock (_gate)
        {
            _last = json;
            foreach (var stream in _streams) stream.Enqueue(json);
        }
    }

    public void SetFiles(IReadOnlyDictionary<string, TvFile> files)
    {
        var copy = new Dictionary<string, TvFile>(files, StringComparer.Ordinal);
        lock (_gate) _files = copy;
    }

    public ValueTask DisposeAsync() => new(StopAsync());

    private async Task AcceptLoopAsync(TcpListener listener, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await listener.AcceptTcpClientAsync(ct);
            }
            catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException or SocketException)
            {
                return;
            }
            lock (_gate)
            {
                if (_connections.Count >= MaxConnections)
                {
                    client.Dispose();
                    continue;
                }
                EnableKeepAlive(client.Client);
                var connection = ServeAsync(client, ct);
                _connections.Add(connection);
                _ = connection.ContinueWith(done => { lock (_gate) _connections.Remove(done); }, TaskScheduler.Default);
            }
        }
    }

    private async Task ServeAsync(TcpClient client, CancellationToken ct)
    {
        await Task.Yield();
        using (client)
        {
            try
            {
                var stream = client.GetStream();
                using var head = CancellationTokenSource.CreateLinkedTokenSource(ct);
                head.CancelAfter(HeadTimeout);
                var (status, request) = await TvHttpRequest.ReadAsync(stream, head.Token);
                if (status == TvReadStatus.Closed) return;
                if (request is null)
                {
                    await TvHttpResponse.WriteStatusAsync(stream, 400, ct);
                    await DrainAsync(client, ct);
                    return;
                }
                await RouteAsync(request, stream, ct);
            }
            catch (Exception ex) when (ex is IOException or OperationCanceledException or ObjectDisposedException or SocketException)
            {
                // Cliente caído, petición lenta o servidor parándose: se cierra la conexión sin más.
            }
            catch (Exception ex)
            {
                _logger.LogError("Fallo al atender una petición de la TV ({ExceptionType}).", ex.GetType().FullName);
            }
        }
    }

    // Cerrar con datos sin leer manda un RST que puede borrar la respuesta antes de que el cliente la lea.
    private static async Task DrainAsync(TcpClient client, CancellationToken ct)
    {
        client.Client.Shutdown(SocketShutdown.Send);
        using var drain = CancellationTokenSource.CreateLinkedTokenSource(ct);
        drain.CancelAfter(TimeSpan.FromSeconds(1));
        var buffer = new byte[4096];
        for (var total = 0; total < 64 * 1024;)
        {
            var read = await client.GetStream().ReadAsync(buffer, drain.Token);
            if (read == 0) return;
            total += read;
        }
    }

    // Una TV que se apaga sin cerrar la conexión no manda FIN: el keepalive de TCP la detecta en ~25 s.
    private static void EnableKeepAlive(Socket socket)
    {
        try
        {
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
            socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, 10);
            socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval, 5);
            socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveRetryCount, 3);
        }
        catch (Exception ex) when (ex is SocketException or PlatformNotSupportedException)
        {
            // Sin ajuste fino del keepalive queda el plazo de escritura del canal de eventos.
        }
    }

    // Un suscriptor que falla no debe cortar la conexión que lo ha provocado ni dejar el recuento a medias.
    private void RaiseConnectionsChanged()
    {
        try
        {
            ConnectionsChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError("Fallo un suscriptor de ConnectionsChanged ({ExceptionType}).", ex.GetType().FullName);
        }
    }

    private void RaiseViewportReported(TvViewport viewport)
    {
        try
        {
            ViewportReported?.Invoke(this, viewport);
        }
        catch (Exception ex)
        {
            _logger.LogError("Fallo un suscriptor de ViewportReported ({ExceptionType}).", ex.GetType().FullName);
        }
    }
}
