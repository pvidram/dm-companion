namespace DmCompanion.Core.Tv;

/// <summary>Servidor HTTP de la TV en la red local: página, canal de empuje (SSE) y ficheros de la lista blanca.</summary>
public interface ITvServer : IAsyncDisposable
{
    bool IsRunning { get; }

    /// <summary>Canales de eventos abiertos (TVs conectadas).</summary>
    int ConnectedCount { get; }

    /// <summary>Cambia ConnectedCount. Puede llegar desde cualquier hilo.</summary>
    event EventHandler? ConnectionsChanged;

    /// <summary>Una TV informa del tamaño de su ventana. Puede llegar desde cualquier hilo.</summary>
    event EventHandler<TvViewport>? ViewportReported;

    /// <summary>Empieza a escuchar con ese código. Lanza <see cref="IOException"/> si todos los puertos están ocupados.</summary>
    Task<TvEndpoint> StartAsync(string code, CancellationToken ct = default);

    Task StopAsync();

    /// <summary>Último mensaje; se manda a todas las TVs y a las que conecten después.</summary>
    void Publish(string json);

    /// <summary>Lista blanca: id → fichero. Sustituye a la anterior.</summary>
    void SetFiles(IReadOnlyDictionary<string, TvFile> files);
}
