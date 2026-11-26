namespace DmCompanion.Desktop;

/// <summary>La segunda instancia pide a la primera que traiga su ventana al frente.</summary>
public static class FocusRequest
{
    private const int Attempts = 3;
    private static readonly TimeSpan AttemptTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan Pause = TimeSpan.FromMilliseconds(500);

    public static async Task<bool> SendAsync(HttpClient http, string instanceUrl, TimeProvider time, CancellationToken ct)
    {
        var target = new Uri(new Uri(instanceUrl), DesktopEndpoints.FocusPath);
        for (var attempt = 1; attempt <= Attempts; attempt++)
        {
            // Una primera instancia colgada no debe bloquear a la segunda: cada intento tiene su propio límite.
            using var timeout = new CancellationTokenSource(AttemptTimeout, time);
            using var attemptCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token);
            try
            {
                using var response = await http.GetAsync(target, attemptCts.Token);
                if (response.IsSuccessStatusCode) return true;
            }
            catch (HttpRequestException)
            {
                // La primera instancia aún no responde: se reintenta tras una pausa.
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                // El intento superó su tiempo límite: se cuenta como fallido.
            }
            if (attempt < Attempts) await Task.Delay(Pause, time, ct);
        }
        return false;
    }
}
