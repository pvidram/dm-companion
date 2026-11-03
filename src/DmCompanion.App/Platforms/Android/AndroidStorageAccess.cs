using Android.Content;
using Android.Provider;
using DmCompanion.Screens.Platform;
using Microsoft.Extensions.Logging;
using AndroidEnvironment = Android.OS.Environment;
using AndroidUri = Android.Net.Uri;

namespace DmCompanion.App;

public sealed class AndroidStorageAccess(ILogger<AndroidStorageAccess> logger) : IStorageAccess
{
    public bool HasAllFilesAccess => AndroidEnvironment.IsExternalStorageManager;

    public string DefaultBrowseRoot => "/storage/emulated/0";

    public void RequestAllFilesAccess()
    {
        var context = Platform.CurrentActivity ?? Android.App.Application.Context;
        var uri = AndroidUri.Parse($"package:{context.PackageName}");
        try
        {
            context.StartActivity(NewIntent(Settings.ActionManageAppAllFilesAccessPermission, uri));
        }
        catch (ActivityNotFoundException)
        {
            // Algunos fabricantes no exponen la pantalla por app: se abre la lista general.
            try
            {
                context.StartActivity(NewIntent(Settings.ActionManageAllFilesAccessPermission, null));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "No se pudo abrir los ajustes de acceso a archivos.");
            }
        }
    }

    private static Intent NewIntent(string action, AndroidUri? uri)
    {
        var intent = uri is null ? new Intent(action) : new Intent(action, uri);
        intent.AddFlags(ActivityFlags.NewTask);
        return intent;
    }
}
