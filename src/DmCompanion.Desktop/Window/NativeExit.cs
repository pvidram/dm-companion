using System.Runtime.InteropServices;

namespace DmCompanion.Desktop.Window;

/// <summary>
/// On Linux, WebKitGTK aborts the process (exit code 134) in its native destructors during exit(), after Main has
/// returned and the host has stopped cleanly. Once everything is saved and the logs are closed, the process ends
/// with _exit, which skips those destructors. Nothing of ours runs after that point.
/// </summary>
public static partial class NativeExit
{
    public static bool ShouldSkipNativeTeardown(bool isLinux, bool windowClosedNormally) => isLinux && windowClosedNormally;

    public static void Now(int code) => Exit(code);

    [LibraryImport("libc", EntryPoint = "_exit")]
    private static partial void Exit(int code);
}
