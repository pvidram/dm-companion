using System.Text;
using System.Threading.Channels;

namespace DmCompanion.Infrastructure.Tv;

/// <summary>
/// Un canal SSE abierto. Un solo escritor por conexión (mensajes en cola y «: ping» si no hay nada que mandar),
/// así que nunca se intercalan escrituras. Como cada mensaje es el estado completo, si la TV se retrasa se descartan los viejos.
/// Cada escritura tiene un plazo: si la TV deja de leer (o desaparece sin cerrar), se da por caída.
/// </summary>
internal sealed class TvEventStream(Stream stream, TimeSpan writeTimeout)
{
    private static readonly byte[] Ping = ": ping\n\n"u8.ToArray();

    private readonly Channel<string> _queue = Channel.CreateBounded<string>(
        new BoundedChannelOptions(4) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });

    public void Enqueue(string json) => _queue.Writer.TryWrite(json);

    /// <summary>Escribe hasta que la TV cierra, falla la escritura o se cancela. Nunca lanza por un cliente caído.</summary>
    public async Task RunAsync(TimeSpan pingInterval, CancellationToken ct)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var reading = WatchForCloseAsync(linked);
        try
        {
            await WriteLoopAsync(pingInterval, linked.Token);
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException or ObjectDisposedException)
        {
            // La TV se ha ido o el servidor se para: el que llama la quita de la lista.
        }
        finally
        {
            await linked.CancelAsync();
            await reading;
        }
    }

    private async Task WriteLoopAsync(TimeSpan pingInterval, CancellationToken ct)
    {
        while (true)
        {
            using var wait = CancellationTokenSource.CreateLinkedTokenSource(ct);
            wait.CancelAfter(pingInterval);
            bool hasMessage;
            try
            {
                hasMessage = await _queue.Reader.WaitToReadAsync(wait.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                await WriteAsync(Ping, ct);
                continue;
            }
            if (!hasMessage) return;
            while (_queue.Reader.TryRead(out var json))
                await WriteAsync(Encode(json), ct);
        }
    }

    private async Task WriteAsync(byte[] bytes, CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(writeTimeout);
        await stream.WriteAsync(bytes, deadline.Token);
        await stream.FlushAsync(deadline.Token);
    }

    // La TV no manda nada por este canal: leer 0 bytes (o un error) significa que ha cerrado.
    private async Task WatchForCloseAsync(CancellationTokenSource writer)
    {
        var buffer = new byte[256];
        try
        {
            while (await stream.ReadAsync(buffer, writer.Token) > 0) { }
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException or ObjectDisposedException)
        {
            // Mismo final que un cierre limpio.
        }
        await writer.CancelAsync();
    }

    internal static byte[] Encode(string json)
    {
        var builder = new StringBuilder();
        foreach (var line in json.Split('\n')) builder.Append("data: ").Append(line.TrimEnd('\r')).Append('\n');
        builder.Append('\n');
        return Encoding.UTF8.GetBytes(builder.ToString());
    }
}
