using System.ComponentModel;
using System.Diagnostics;
using DmCompanion.Screens.Platform;

namespace DmCompanion.Desktop.Platform;

public sealed class DesktopLogFolder : ILogFolder
{
    private readonly DesktopPaths _paths;
    private readonly ILogger<DesktopLogFolder> _logger;
    private readonly Func<ProcessStartInfo, Process?> _start;

    public DesktopLogFolder(DesktopPaths paths, ILogger<DesktopLogFolder> logger)
        : this(paths, logger, Process.Start)
    {
    }

    // El arranque del proceso se inyecta para poder probar el fallo sin lanzar el Explorador.
    internal DesktopLogFolder(DesktopPaths paths, ILogger<DesktopLogFolder> logger, Func<ProcessStartInfo, Process?> start)
    {
        _paths = paths;
        _logger = logger;
        _start = start;
    }

    public bool IsAvailable => OperatingSystem.IsWindows();

    public bool Open()
    {
        try
        {
            Directory.CreateDirectory(_paths.LogsFolder);
            using var _ = _start(ExplorerStartInfo(_paths.LogsFolder));
            return true;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            _logger.LogWarning("No se pudo abrir la carpeta de registros ({Error}).", ex.GetType().Name);
            return false;
        }
    }

    // ArgumentList entrecomilla la ruta: con espacios en el nombre de usuario el Explorador abriría otra carpeta.
    internal static ProcessStartInfo ExplorerStartInfo(string folder)
    {
        var info = new ProcessStartInfo { FileName = "explorer.exe" };
        info.ArgumentList.Add(folder);
        return info;
    }
}
