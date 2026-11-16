using DmCompanion.Core.Glossary;
using DmCompanion.Core.Vault;

namespace DmCompanion.Screens.Services;

// Contenido en la TV: una ficha de PJ o una nota de lugar a pantalla completa, hasta «Volver al mapa».
public sealed partial class TvSession
{
    private sealed record ShownContent(string? Path, string? GlossaryKey, string Title, VaultService? Vault);

    // Inmutable para que la UI lo lea sin el gate.
    private ShownContent? _showing;

    /// <summary>Nota que muestra la TV en modo Content; null en los demás modos.</summary>
    public string? ContentPath => Mode == TvMode.Content ? _showing?.Path : null;

    /// <summary>Título de la nota que muestra la TV en modo Content; null en los demás modos.</summary>
    public string? ContentTitle => Mode == TvMode.Content ? _showing?.Title : null;

    /// <summary>
    /// Muestra en la TV esa ficha de PJ o nota de lugar, filtrada para los jugadores. No hace nada con la TV apagada ni si la
    /// nota no existe, es secreta o no es de PJ ni de lugar.
    /// </summary>
    public async Task ShowContentAsync(string notePath)
    {
        await _content.PrepareAsync();
        await _gate.WaitAsync();
        try
        {
            if (Mode == TvMode.Off || _content.Build(notePath) is not { } message) return;
            _showing = new ShownContent(notePath, null, message.Title, _host.Current);
            Mode = TvMode.Content;
            Send(message.Json, message.Files);
        }
        finally
        {
            _gate.Release();
        }
        Notify();
    }

    /// <summary>Muestra en la TV esa entrada del SRD (no las criaturas). No hace nada con la TV apagada.</summary>
    public async Task ShowGlossaryAsync(GlossaryEntry entry)
    {
        if (!TvContentFeed.CanShow(entry)) return;
        await _gate.WaitAsync();
        try
        {
            if (Mode == TvMode.Off) return;
            var message = _content.BuildGlossary(entry);
            _showing = new ShownContent(null, entry.Key, message.Title, _host.Current);
            Mode = TvMode.Content;
            Send(message.Json, message.Files);
        }
        finally
        {
            _gate.Release();
        }
        Notify();
    }

    /// <summary>Saca a la TV del contenido: vuelve al último mapa (con sus cambios mientras tanto) o, si no hay, a la espera.</summary>
    public async Task BackToMapAsync()
    {
        await _gate.WaitAsync();
        try
        {
            if (Mode != TvMode.Content) return;
            LeaveContent();
        }
        finally
        {
            _gate.Release();
        }
        Notify();
    }

    // Llamar con _gate tomado, tras un cambio del vault. La nota mostrada se vuelve a generar (Send descarta el mensaje si no
    // cambió); si se borró, se volvió secreta, cambió de tipo o se abrió otro vault, la TV vuelve al mapa. True si hay que avisar.
    private bool RefreshContent()
    {
        if (Mode != TvMode.Content || _showing is not { } shown) return false;
        if (shown.GlossaryKey is { } key)
        {
            // El SRD no cambia con el vault: solo se abandona si el vault o el sistema dejan de ofrecerlo.
            if (ReferenceEquals(_host.Current, shown.Vault) && GlossaryPacks.CategoryOfKey(key) is { } category
                && _host.Current?.ActiveSystem.GlossaryPacks.Contains(category) == true) return false;
            LeaveContent();
            return true;
        }
        if (shown.Path is { } path && ReferenceEquals(_host.Current, shown.Vault) && _content.Build(path) is { } message)
        {
            _showing = shown with { Title = message.Title };
            Send(message.Json, message.Files);
            return message.Title != shown.Title;
        }
        LeaveContent();
        return true;
    }

    // Una imagen del vault puede llegar después de la edición de la nota que la cita (Syncthing escribe en cualquier orden), o
    // sustituir a otra en la misma ruta, y eso no lanza VaultHost.Changed: con la lista ya recargada se vuelven a generar el
    // mapa (también el guardado para «Volver al mapa») y el contenido, y una imagen sustituida recibe un id nuevo.
    private void OnImagesChanged(object? sender, EventArgs e)
    {
        if (Mode is not (TvMode.Map or TvMode.Content)) return;
        _ = UnderGateAsync(() =>
        {
            if (!TvMapIsGone()) RefreshMap();
            return RefreshContent();
        }, "No se pudo actualizar la TV tras cambiar una imagen ({ExceptionType}).", _content.PrepareAsync);
    }

    // Llamar con _gate tomado.
    private void LeaveContent()
    {
        _showing = null;
        if (LastMap is { } map)
        {
            Mode = TvMode.Map;
            Send(map.Json, map.Files);
        }
        else
        {
            Mode = TvMode.Waiting;
            PublishWaiting();
        }
    }
}
