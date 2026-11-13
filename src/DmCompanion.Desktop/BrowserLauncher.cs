using System.Diagnostics;

namespace DmCompanion.Desktop;

public interface IBrowserLauncher
{
    void Open(string url);
}

public sealed class BrowserLauncher(ILogger<BrowserLauncher> logger) : IBrowserLauncher
{
    public void Open(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true })?.Dispose();
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or PlatformNotSupportedException)
        {
            logger.LogWarning("No se pudo abrir el navegador ({Error}). Abre {Url} a mano.", ex.GetType().Name, url);
        }
    }
}
