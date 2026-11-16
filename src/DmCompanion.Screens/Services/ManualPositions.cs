using System.Globalization;
using DmCompanion.Core.Manuals;
using DmCompanion.Screens.Platform;

namespace DmCompanion.Screens.Services;

/// <summary>Última página abierta de cada manual, en las preferencias de la app (no en el vault).</summary>
public sealed class ManualPositions(IAppPreferences prefs)
{
    public int? LastPage(string pdfPath) =>
        int.TryParse(prefs.Get(ManualPaths.PositionKeyOf(pdfPath)), NumberStyles.None, CultureInfo.InvariantCulture, out var page) && page >= 1
            ? page
            : null;

    public void Remember(string pdfPath, int page)
    {
        if (page >= 1) prefs.Set(ManualPaths.PositionKeyOf(pdfPath), page.ToString(CultureInfo.InvariantCulture));
    }
}
