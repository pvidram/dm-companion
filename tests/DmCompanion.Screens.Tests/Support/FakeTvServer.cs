using DmCompanion.Core.Tv;

namespace DmCompanion.Screens.Tests.Support;

/// <summary>Servidor de la TV en memoria: guarda lo publicado y deja simular conexiones y puertos ocupados.</summary>
internal sealed class FakeTvServer : ITvServer
{
    public const int Port = 8590;

    public bool PortsBusy { get; set; }
    public Exception? StartFailure { get; set; }
    public Exception? StopFailure { get; set; }
    public IReadOnlyList<string> Addresses { get; set; } = ["192.168.1.20", "10.0.0.5"];
    private readonly Lock _lock = new();
    private readonly List<string> _published = [];

    /// <summary>Copia de lo publicado: TvSession publica desde otros hilos mientras el test lee.</summary>
    public IReadOnlyList<string> Published
    {
        get { lock (_lock) return [.. _published]; }
    }
    public string? StartedCode { get; private set; }

    public bool IsRunning { get; private set; }
    public int ConnectedCount { get; private set; }

    public event EventHandler? ConnectionsChanged;
    public event EventHandler<TvViewport>? ViewportReported;

    public Task<TvEndpoint> StartAsync(string code, CancellationToken ct = default)
    {
        if (PortsBusy) throw new IOException("Puertos ocupados.");
        if (StartFailure is not null) throw StartFailure;
        IsRunning = true;
        StartedCode = code;
        return Task.FromResult(new TvEndpoint(Port, Addresses));
    }

    public Task StopAsync()
    {
        if (StopFailure is not null) throw StopFailure;
        IsRunning = false;
        ConnectedCount = 0;
        return Task.CompletedTask;
    }

    public void Publish(string json)
    {
        lock (_lock) _published.Add(json);
    }

    /// <summary>Última lista blanca recibida.</summary>
    public IReadOnlyDictionary<string, TvFile> Files
    {
        get { lock (_lock) return _files; }
    }

    private IReadOnlyDictionary<string, TvFile> _files = new Dictionary<string, TvFile>();

    public void SetFiles(IReadOnlyDictionary<string, TvFile> files)
    {
        lock (_lock) _files = files;
    }

    public void SetConnected(int count)
    {
        ConnectedCount = count;
        ConnectionsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ReportViewport(TvViewport viewport) => ViewportReported?.Invoke(this, viewport);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
