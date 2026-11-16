using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace DmCompanion.Screens.Services;

/// <summary>
/// Único punto de interop del visor de manuales (`manual-viewer.js`, con pdf.js): los componentes nunca inyectan IJSRuntime.
/// Scoped: un visor por circuito. Los errores de JS se registran solo por tipo, porque su mensaje podría citar el PDF.
/// </summary>
public sealed class ManualViewerInterop(IJSRuntime js, ILogger<ManualViewerInterop> logger) : IAsyncDisposable
{
    public const string ModulePath = "./_content/DmCompanion.Screens/js/manual-viewer.js";

    // Tope del PNG para la TV: 1920×1080 en RGBA sin comprimir ocupa ~8 MB, y el PNG siempre menos.
    private const long MaxPngBytes = 16 * 1024 * 1024;

    private Task<IJSObjectReference>? _module;
    private DotNetObjectReference<ManualViewerCallbacks>? _callbacks;

    private const string VaultUrlPrefix = "/_vault/";

    /// <summary>
    /// El usuario deslizó en horizontal: 1 pide la página siguiente y -1 la anterior.
    /// Se dispara desde el interop JS, fuera del dispatcher de Blazor: el componente debe usar <c>InvokeAsync</c> para tocar su estado.
    /// </summary>
    public event Action<int>? PageTurnRequested;

    /// <summary>Abre el PDF en el host (un div) y pinta la página; null si no se pudo abrir (y loguea el tipo de error).</summary>
    public async Task<ManualDocInfo?> OpenAsync(ElementReference host, string vaultUrl, int page)
    {
        // Solo ficheros del vault: el visor nunca abre otra URL.
        if (!vaultUrl.StartsWith(VaultUrlPrefix, StringComparison.Ordinal)) return null;
        _callbacks?.Dispose();
        _callbacks = DotNetObjectReference.Create(new ManualViewerCallbacks(direction => PageTurnRequested?.Invoke(direction)));
        try
        {
            var module = await LoadModuleAsync();
            // Sin el tiempo máximo por defecto de Blazor Server: un PDF grande puede tardar en abrir.
            return await module.InvokeAsync<ManualDocInfo?>("open", CancellationToken.None, host, vaultUrl, page, _callbacks);
        }
        catch (Exception ex) when (IsInteropFailure(ex))
        {
            if (ex is not JSDisconnectedException) logger.LogWarning("No se pudo abrir el manual ({ExceptionType}).", ex.GetType().FullName);
            return null;
        }
    }

    /// <summary>Pinta la página; false si no se pudo ni tras el reintento a menor escala (o si falló el interop).</summary>
    public async Task<bool> ShowPageAsync(int page)
    {
        try
        {
            var module = await LoadModuleAsync();
            return await module.InvokeAsync<bool>("showPage", CancellationToken.None, page);
        }
        catch (Exception ex) when (IsInteropFailure(ex))
        {
            if (ex is not JSDisconnectedException) logger.LogWarning("No se pudo mostrar la página del manual ({ExceptionType}).", ex.GetType().FullName);
            return false;
        }
    }

    /// <summary>Busca página a página; llama a onHit por cada acierto; devuelve false si se canceló.</summary>
    public async Task<bool> SearchAsync(string text, Func<ManualSearchHit, Task> onHit, CancellationToken ct)
    {
        if (ct.IsCancellationRequested) return false;
        using var session = DotNetObjectReference.Create(new ManualSearchSession(onHit, ct));
        try
        {
            var module = await LoadModuleAsync();
            // La cancelación la consulta el JS entre páginas (isCancelled), así que aquí no se pasa el token.
            var completed = await module.InvokeAsync<bool>("search", CancellationToken.None, text, session);
            return completed && !ct.IsCancellationRequested;
        }
        catch (Exception ex) when (IsInteropFailure(ex))
        {
            if (ex is not JSDisconnectedException) logger.LogWarning("Falló la búsqueda en el manual ({ExceptionType}).", ex.GetType().FullName);
            return false;
        }
    }

    /// <summary>PNG de la página (o del recorte) a la escala que llena maxWidth × maxHeight; null si falla.</summary>
    public async Task<byte[]?> RenderPngAsync(int page, CropRect? crop, int maxWidth, int maxHeight)
    {
        try
        {
            var module = await LoadModuleAsync();
            // Como flujo y no como byte[]: Blazor Server corta los mensajes de más de 32 KB.
            await using var reference = await module.InvokeAsync<IJSStreamReference>("renderPng", CancellationToken.None, page, crop, maxWidth, maxHeight);
            await using var stream = await reference.OpenReadStreamAsync(MaxPngBytes);
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer);
            return buffer.ToArray();
        }
        catch (Exception ex) when (IsInteropFailure(ex) || ex is IOException or InvalidOperationException)
        {
            if (ex is not JSDisconnectedException) logger.LogWarning("No se pudo generar la imagen de la página ({ExceptionType}).", ex.GetType().FullName);
            return null;
        }
    }

    /// <summary>Entra en el modo recorte; termina con <see cref="ConfirmCropAsync"/> (el rectángulo) o <see cref="CancelCropAsync"/> (null).</summary>
    public async Task<CropRect?> PickCropAsync()
    {
        try
        {
            var module = await LoadModuleAsync();
            // Espera al usuario: sin tiempo máximo.
            return await module.InvokeAsync<CropRect?>("pickCrop", CancellationToken.None);
        }
        catch (Exception ex) when (IsInteropFailure(ex))
        {
            if (ex is not JSDisconnectedException) logger.LogWarning("Falló el modo recorte del manual ({ExceptionType}).", ex.GetType().FullName);
            return null;
        }
    }

    /// <summary>«Enviar»: termina el modo recorte con el rectángulo dibujado (null si no se dibujó ninguno).</summary>
    public Task ConfirmCropAsync() => TryInvokeVoidAsync("confirmCrop");

    /// <summary>«Cancelar»: termina el modo recorte sin rectángulo.</summary>
    public Task CancelCropAsync() => TryInvokeVoidAsync("cancelCrop");

    /// <summary>destroy() del documento, limpia canvas y listeners.</summary>
    public async Task CloseAsync()
    {
        if (_module is not null) await TryInvokeVoidAsync("close");
        _callbacks?.Dispose();
        _callbacks = null;
    }

    public async ValueTask DisposeAsync()
    {
        await CloseAsync();
        if (_module is null) return;
        try
        {
            await (await _module).DisposeAsync();
        }
        catch (Exception ex) when (IsInteropFailure(ex))
        {
            // El circuito ya se cerró o el módulo nunca llegó a cargarse: no hay nada que liberar.
        }
    }

    // Si la importación falla, no se cachea el fallo: la siguiente llamada vuelve a intentarlo.
    private async Task<IJSObjectReference> LoadModuleAsync()
    {
        var pending = _module ??= js.InvokeAsync<IJSObjectReference>("import", ModulePath).AsTask();
        try
        {
            return await pending;
        }
        catch
        {
            if (ReferenceEquals(_module, pending)) _module = null;
            throw;
        }
    }

    private async Task TryInvokeVoidAsync(string identifier, params object?[] args)
    {
        try
        {
            var module = await LoadModuleAsync();
            await module.InvokeVoidAsync(identifier, CancellationToken.None, args);
        }
        catch (Exception ex) when (IsInteropFailure(ex))
        {
            if (ex is not JSDisconnectedException) logger.LogWarning("Falló la llamada {Identifier} del visor de manuales ({ExceptionType}).", identifier, ex.GetType().FullName);
        }
    }

    // JSDisconnectedException: el circuito ya se cerró, no es un fallo que avisar.
    private static bool IsInteropFailure(Exception ex) => ex is JSException or JSDisconnectedException or TaskCanceledException;
}
