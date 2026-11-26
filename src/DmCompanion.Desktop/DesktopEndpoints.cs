using System.Net;
using DmCompanion.Desktop.Window;

namespace DmCompanion.Desktop;

/// <summary>Endpoints locales del host de escritorio. Solo atienden peticiones desde loopback.</summary>
public static class DesktopEndpoints
{
    public const string FocusPath = "/_desktop/focus";
    public const string OpenExternalPath = "/_desktop/open-external";

    public static IEndpointRouteBuilder MapDesktopEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet(FocusPath, (HttpContext context, IWindowActivator window) =>
        {
            if (!IsLoopback(context)) return Results.StatusCode(StatusCodes.Status403Forbidden);
            window.BringToFront();
            return Results.NoContent();
        });

        // JSON obligatorio: un formulario o un text/plain de otra web no pasan sin preflight, y no hay CORS.
        app.MapPost(OpenExternalPath, (HttpContext context, OpenExternalBody? body, IBrowserLauncher launcher) =>
        {
            if (!IsLoopback(context) || !IsSameOrigin(context)) return Results.StatusCode(StatusCodes.Status403Forbidden);
            if (body?.Url is not { } url
                || !Uri.TryCreate(url, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                return Results.BadRequest();
            launcher.Open(uri.AbsoluteUri);
            return Results.NoContent();
        });

        return app;
    }

    private static bool IsLoopback(HttpContext context) =>
        context.Connection.RemoteIpAddress is { } remote && IPAddress.IsLoopback(remote);

    // Sin cabecera Origin (petición no cruzada) se acepta; con ella, debe ser la propia app.
    private static bool IsSameOrigin(HttpContext context)
    {
        var origin = context.Request.Headers.Origin.ToString();
        return origin.Length == 0
            || (Uri.TryCreate(origin, UriKind.Absolute, out var uri) && uri.Authority == context.Request.Host.Value);
    }
}

/// <summary>Cuerpo JSON de la petición de abrir un enlace externo.</summary>
internal sealed record OpenExternalBody(string? Url);
