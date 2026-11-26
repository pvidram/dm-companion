using DmCompanion.Core.Maps;

namespace DmCompanion.Screens.Services;

// Cambios del usuario sobre el mapa abierto y sus avisos.
public sealed partial class MapSession
{
    public void Mutate(Func<MapEditor, bool> change)
    {
        lock (_lock)
        {
            if (_disposed || IsReadOnly || Editor is not { } editor || !change(editor)) return;
            _revision++;
            ScheduleSave();
        }
        RaiseChanged();
    }

    // Un onload tardío del mapa A no debe aplicarse al B: se exige la nota con la que se cargó la imagen.
    public void SetImageSize(string notePath, double width, double height)
    {
        lock (_lock)
        {
            if (Editor is not { } editor || Current is not { } current || current.NotePath != notePath) return;
            if (current.Width == width && current.Height == height) return;
            Current = current with { Width = width, Height = height };
            Editor = new MapEditor(editor.State, width, height, NewId);
        }
        RaiseChanged();
    }

    /// <summary>El lienzo no pudo cargar la imagen del mapa. Aviso aparte de <see cref="Notice"/>: ninguno pisa al otro.</summary>
    public void ReportImageMissing()
    {
        lock (_lock) ImageNotice = "No se encuentra la imagen del mapa";
        RaiseChanged();
    }

    /// <summary>El usuario cierra el aviso actual del mapa. Un fallo de guardado no se cierra: lleva la vía de escape «Descartar cambios» y solo se va al guardar bien o al descartar.</summary>
    public void DismissNotice()
    {
        lock (_lock)
        {
            if (Notice is null || _failureNotice) return;
            Notice = null;
        }
        RaiseChanged();
    }

    /// <summary>El usuario cierra el aviso de imagen no encontrada.</summary>
    public void DismissImageNotice()
    {
        lock (_lock)
        {
            if (ImageNotice is null) return;
            ImageNotice = null;
        }
        RaiseChanged();
    }
}
