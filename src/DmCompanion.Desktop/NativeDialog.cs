using System.Runtime.InteropServices;

namespace DmCompanion.Desktop;

/// <summary>Diálogo de error del sistema para cuando no hay ventana de la app (Windows) o no hay escritorio (stderr).</summary>
public static class NativeDialog
{
    private const uint MbOk = 0x0;
    private const uint MbIconError = 0x10;

    public static void ShowError(string message)
    {
        if (OperatingSystem.IsWindows()) MessageBoxW(IntPtr.Zero, message, "DM Companion", MbOk | MbIconError);
        else Console.Error.WriteLine(message);
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(IntPtr owner, string text, string caption, uint type);
}
