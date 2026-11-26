using System.Drawing;
using Photino.NET;

namespace DmCompanion.Desktop.Window;

/// <summary>
/// La ventana nativa de la app. Restaura y guarda su posición, se trae al frente a petición de una segunda instancia
/// y se cierra con un aviso si el host se para por su cuenta. No tiene tests: necesita un escritorio.
/// </summary>
public sealed class DesktopWindow(WindowPlacementStore store, DesktopPaths paths, ILogger<DesktopWindow> logger) : IWindowActivator
{
    // Se publica solo cuando la ventana nativa ya existe y se retira antes de que empiece a cerrarse.
    private volatile PhotinoWindow? _window;
    private volatile bool _closeSilently;
    private WindowBounds? _lastNormal;

    /// <summary>Un apagado pedido desde fuera (SIGTERM, Ctrl+C) cierra la ventana sin el aviso de «se ha detenido».</summary>
    public void CloseSilentlyOnStop() => _closeSilently = true;

    public void Run(string url, CancellationToken hostStopping)
    {
        CancellationTokenRegistration stopping = default;
        var window = new PhotinoWindow()
            .SetTitle("DM Companion")
            .SetIconFile(Path.Combine(AppContext.BaseDirectory, "Assets", OperatingSystem.IsWindows() ? "dm-companion.ico" : "dm-companion.png"))
            .SetUseOsDefaultSize(false)
            .SetUseOsDefaultLocation(false)
            .SetMinSize(WindowPlacement.MinWidth, WindowPlacement.MinHeight)
#if DEBUG
            .SetDevToolsEnabled(true)
            .SetContextMenuEnabled(true)
#else
            .SetDevToolsEnabled(false)
            .SetContextMenuEnabled(false)
#endif
            .Load(url);

        window.RegisterWindowCreatedHandler((_, _) =>
        {
            ApplyPlacement(window);
            _window = window;
            // Solo con la ventana ya creada y mientras está abierta: si el host se para por su cuenta, se avisa y se cierra.
            stopping = hostStopping.Register(() => CloseOnHostStop(window));
        });
        window.RegisterSizeChangedHandler((_, _) => RememberNormal(window));
        window.RegisterLocationChangedHandler((_, _) => RememberNormal(window));
        window.RegisterWindowClosingHandler((_, _) =>
        {
            _window = null;
            Save(window);
            return false; // false = no cancelar el cierre
        });

        try
        {
            window.WaitForClose();
        }
        finally
        {
            _window = null;
            stopping.Unregister();
        }
    }

    public void BringToFront()
    {
        if (_window is not { } window) return;
        TryInvoke(window, () =>
        {
            if (window.Minimized) window.SetMinimized(false);
            // Photino no tiene «activar»: subir y bajar TopMost la trae delante en Windows y en GTK.
            window.SetTopMost(true);
            window.SetTopMost(false);
        });
    }

    // StopApplication llama a este callback de forma síncrona: el Invoke (que espera al hilo de la ventana) va a otro hilo
    // para no bloquear a quien paró el host si la ventana se está cerrando a la vez.
    private void CloseOnHostStop(PhotinoWindow window) => ThreadPool.QueueUserWorkItem(_ => TryInvoke(window, () =>
    {
        if (!_closeSilently)
            window.ShowMessage("DM Companion", $"DM Companion se ha detenido; revisa los registros en {paths.LogsFolder}.", PhotinoDialogButtons.Ok, PhotinoDialogIcon.Error);
        window.Close();
    }));

    // La ventana puede estar cerrándose: un Invoke fallido se registra (solo el tipo) y se ignora.
    private void TryInvoke(PhotinoWindow window, Action action)
    {
        try
        {
            window.Invoke(action);
        }
        catch (Exception ex)
        {
            logger.LogDebug("No se pudo ejecutar la acción en la ventana ({ExceptionType}).", ex.GetType().FullName);
        }
    }

    private void ApplyPlacement(PhotinoWindow window)
    {
        var screens = window.Monitors.Select(m => new ScreenArea(m.WorkArea.X, m.WorkArea.Y, m.WorkArea.Width, m.WorkArea.Height)).ToList();
        if (WindowPlacement.Restore(store.Load(), screens) is { } bounds)
        {
            window.SetLeft(bounds.X).SetTop(bounds.Y).SetSize(new Size(bounds.Width, bounds.Height));
            if (bounds.Maximized) window.SetMaximized(true);
        }
        else
        {
            window.SetMaximized(true);
        }
    }

    // Se recuerda el último tamaño no maximizado para restaurarlo aunque se cierre maximizada.
    private void RememberNormal(PhotinoWindow window)
    {
        if (!window.Maximized && !window.Minimized)
            _lastNormal = new WindowBounds(window.Left, window.Top, window.Width, window.Height, false);
    }

    private void Save(PhotinoWindow window)
    {
        var normal = _lastNormal ?? new WindowBounds(window.Left, window.Top, window.Width, window.Height, false);
        store.Save(normal with { Maximized = window.Maximized });
        logger.LogDebug("Posición de la ventana guardada.");
    }
}
