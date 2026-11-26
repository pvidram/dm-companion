using System.Net.Http.Headers;
using System.Text.Json;
using DmCompanion.Screens.Platform;
using DmCompanion.Screens.Services;

namespace DmCompanion.Desktop.Updates;

/// <summary>Última release publicada en GitHub. No envía nada salvo el User-Agent; cualquier fallo devuelve null.</summary>
public sealed class GitHubUpdateCheck(HttpClient http, IBrowserLauncher launcher, ILogger<GitHubUpdateCheck> logger) : IUpdateCheck
{
    public const string LatestReleaseUrl = "https://api.github.com/repos/pvidram/dm-companion/releases/latest";

    public async Task<AvailableUpdate?> GetLatestAsync(CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseUrl);
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue("DMCompanion", AppVersion.Current));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            using var response = await http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogInformation("La consulta de versiones respondió {Status}.", (int)response.StatusCode);
                return null;
            }
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            return Read(json.RootElement);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException)
        {
            if (ct.IsCancellationRequested) throw;
            logger.LogInformation("No se pudo consultar la última versión ({ExceptionType}).", ex.GetType().Name);
            return null;
        }
    }

    public void OpenReleasePage(AvailableUpdate update) => launcher.Open(update.Url);

    private AvailableUpdate? Read(JsonElement root)
    {
        var update = Parse(root);
        if (update is null) logger.LogInformation("La respuesta de versiones no trae una versión o una página legibles.");
        return update;
    }

    private static AvailableUpdate? Parse(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object) return null;
        if (!root.TryGetProperty("tag_name", out var tag) || tag.ValueKind != JsonValueKind.String || tag.GetString() is not { } tagName) return null;
        if (!root.TryGetProperty("html_url", out var page) || page.ValueKind != JsonValueKind.String || page.GetString() is not { } pageUrl) return null;
        var version = ReleaseVersion.Normalize(tagName);
        if (ReleaseVersion.Compare(version, version) is null) return null;
        return Uri.TryCreate(pageUrl, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps
            ? new AvailableUpdate(version, pageUrl)
            : null;
    }
}
