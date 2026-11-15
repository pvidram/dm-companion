using DmCompanion.Core.Tv;

namespace DmCompanion.Screens.Services;

// El código fijo de la dirección de la TV: lo elige el autor, se guarda en las preferencias y sobrevive a reinicios.
// Nunca se escribe en los logs.
public sealed partial class TvSession
{
    private const string CodeKey = "tv.code";

    /// <summary>
    /// Código de la dirección: el guardado si es válido o, mientras no haya uno, la propuesta (nombre de la campaña o «mesa»).
    /// Se guarda al activar la TV o al fijarlo; la propuesta puede cambiar hasta entonces.
    /// </summary>
    public string Code => SavedCode() ?? TvCode.Suggest(CampaignName());

    /// <summary>True si ya hay un código guardado: cambiarlo deja sin entrar a las TVs que guardaron la dirección.</summary>
    public bool HasSavedCode => SavedCode() is not null;

    public string? AddressFor(string ip) =>
        Endpoint is { } endpoint ? $"http://{ip}:{endpoint.Port}/tv/{Code}/" : null;

    private string? SavedCode() =>
        _prefs.Get(CodeKey) is { } saved && TvCode.IsValid(saved) ? TvCode.Normalize(saved) : null;

    /// <summary>
    /// Guarda el código. False si no es válido (no cambia nada). Con la TV activada reinicia el servidor con el código nuevo y
    /// vuelve a publicar lo último sin cambiar el modo; las TVs que usaban el código anterior dejan de entrar.
    /// Si al reiniciar no hay puerto libre, el código queda guardado, la TV queda desactivada, se avisa por <see cref="Changed"/> y se propaga <see cref="IOException"/>.
    /// </summary>
    public async Task<bool> SetCodeAsync(string code)
    {
        if (!TvCode.IsValid(code)) return false;
        var normalized = TvCode.Normalize(code);
        var changed = false;
        await _gate.WaitAsync();
        try
        {
            if (normalized == SavedCode()) return true;
            _prefs.Set(CodeKey, normalized);
            changed = true;
            if (Mode != TvMode.Off) await RestartServerAsync(normalized);
        }
        finally
        {
            _gate.Release();
            // También si el reinicio falló: la TV quedó desactivada y la UI tiene que enterarse antes de ver el error.
            if (changed) Notify();
        }
        return true;
    }

    // Llamar con _gate tomado y la TV activada.
    private async Task RestartServerAsync(string code)
    {
        await _server.StopAsync();
        Connected = 0;
        _lastSent = null;
        try
        {
            Endpoint = await _server.StartAsync(code);
        }
        catch
        {
            Mode = TvMode.Off;
            Endpoint = null;
            Viewport = null;
            _showing = null;
            ForgetMaps();
            throw;
        }
        Republish();
    }

    // Llamar con _gate tomado. Publica de nuevo lo que ve la TV en su modo actual (el servidor nuevo empieza sin nada).
    private void Republish()
    {
        if (Mode == TvMode.Content) RefreshContent();
        else if (Mode == TvMode.Map && LastMap is { } map) Send(map.Json, map.Files);
        else PublishWaiting();
    }
}
