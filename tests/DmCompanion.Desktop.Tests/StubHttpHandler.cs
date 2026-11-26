using System.Net;

namespace DmCompanion.Desktop.Tests;

/// <summary>Responde con la función dada y guarda las peticiones recibidas.</summary>
internal sealed class StubHttpHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
{
    public List<HttpRequestMessage> Requests { get; } = [];

    public static StubHttpHandler Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new((_, _) => Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) }));

    public static StubHttpHandler Throws(Exception exception) => new((_, _) => Task.FromException<HttpResponseMessage>(exception));

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        return respond(request, cancellationToken);
    }
}
