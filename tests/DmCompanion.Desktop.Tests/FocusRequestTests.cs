using System.Net;
using Microsoft.Extensions.Time.Testing;

namespace DmCompanion.Desktop.Tests;

public sealed class FocusRequestTests
{
    [Fact]
    public async Task Send_returns_true_when_the_instance_answers()
    {
        var handler = StubHttpHandler.Json("", HttpStatusCode.NoContent);

        var answered = await FocusRequest.SendAsync(new HttpClient(handler), "http://127.0.0.1:5190", TimeProvider.System, CancellationToken.None);

        (answered, handler.Requests.Single().RequestUri!.ToString()).Should().Be((true, "http://127.0.0.1:5190/_desktop/focus"));
    }

    [Fact]
    public async Task Send_retries_three_times_then_gives_up()
    {
        var handler = StubHttpHandler.Throws(new HttpRequestException("sin respuesta"));
        var time = new FakeTimeProvider();

        var sending = FocusRequest.SendAsync(new HttpClient(handler), "http://127.0.0.1:5190", time, CancellationToken.None);

        // El temporizador de Task.Delay se registra de forma asíncrona: se avanza el reloj hasta que termine.
        while (!sending.IsCompleted)
        {
            time.Advance(TimeSpan.FromMilliseconds(500));
            await Task.Delay(5);
        }

        (await sending, handler.Requests.Count).Should().Be((false, 3));
    }

    [Fact]
    public async Task Send_gives_up_after_three_attempts_when_the_instance_hangs()
    {
        var handler = new StubHttpHandler(async (_, ct) =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            return new HttpResponseMessage();
        });
        var time = new FakeTimeProvider();

        var sending = FocusRequest.SendAsync(new HttpClient(handler), "http://127.0.0.1:5190", time, CancellationToken.None);

        while (!sending.IsCompleted)
        {
            time.Advance(TimeSpan.FromMilliseconds(500));
            await Task.Delay(5);
        }

        (await sending, handler.Requests.Count).Should().Be((false, 3));
    }

    [Fact]
    public async Task Send_rethrows_when_the_caller_cancels()
    {
        var handler = new StubHttpHandler(async (_, ct) =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            return new HttpResponseMessage();
        });
        using var cts = new CancellationTokenSource();

        var sending = FocusRequest.SendAsync(new HttpClient(handler), "http://127.0.0.1:5190", new FakeTimeProvider(), cts.Token);
        await cts.CancelAsync();

        await sending.Awaiting(s => s).Should().ThrowAsync<OperationCanceledException>();
    }
}
