using DmCompanion.Core.Vault;
using Microsoft.AspNetCore.Http.Features;
using DmCompanion.Screens.Services;

namespace DmCompanion.DevHost;

public static class VaultFileEndpoint
{
    public static IEndpointRouteBuilder MapVaultFiles(this IEndpointRouteBuilder app)
    {
        app.MapGet(VaultFileRequest.Prefix + "{**path}", async (HttpContext context, VaultHost host) =>
        {
            // Se valida sobre el destino crudo (sin decodificar), igual que en la App: Request.Path ya viene
            // decodificado por Kestrel y decodificarlo otra vez sería doble decodificación.
            var raw = context.Features.Get<IHttpRequestFeature>()?.RawTarget;
            // El servidor en memoria (TestServer) no rellena RawTarget: ahí se recodifica la ruta.
            if (string.IsNullOrEmpty(raw)) raw = context.Request.Path.ToUriComponent();
            if (host.Current is not { } vault
                || !VaultFileRequest.TryParse(raw, out var relative))
                return Results.NotFound();
            try
            {
                var stream = await vault.OpenReadAsync(relative, context.RequestAborted);
                return Results.Stream(stream, VaultFileRequest.ContentTypeOf(relative), enableRangeProcessing: true);
            }
            catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException or UnauthorizedAccessException or ArgumentException)
            {
                return Results.NotFound();
            }
        });
        return app;
    }
}
