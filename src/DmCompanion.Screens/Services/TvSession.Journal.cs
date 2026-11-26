using DmCompanion.Core.Journal;

namespace DmCompanion.Screens.Services;

/// <summary>El pase de diapositivas en curso: un título, las diapositivas (una foto del momento) y la que se ve.</summary>
public sealed record JournalPlayback(string Title, bool IsLog, IReadOnlyList<JournalSlide> Slides, int Position)
{
    public JournalSlide Current => Slides[Position];
    public bool HasPrevious => Position > 0;
    public bool HasNext => Position < Slides.Count - 1;
}

// Diario en la TV: una entrada (imagen a imagen y su nota) o la bitácora entera como pase de diapositivas.
public sealed partial class TvSession
{
    /// <summary>El pase que muestra la TV en modo Content; null en los demás modos.</summary>
    public JournalPlayback? Journal => Mode == TvMode.Content ? _showing?.Journal : null;

    /// <summary>Muestra en la TV las imágenes de esa entrada y, al final, su nota. No hace nada con la TV apagada ni si no hay nada que mostrar.</summary>
    public Task ShowJournalEntryAsync(JournalEntry entry) =>
        ShowJournalAsync(new JournalPlayback($"{entry.OwnerName} · {entry.Title}", false, JournalSlides.ForEntry(entry), 0));

    /// <summary>Muestra en la TV las imágenes de todas las entradas, en el orden de la bitácora. Las entradas son las que se pasan: no sigue al índice.</summary>
    public Task ShowJournalLogAsync(IEnumerable<JournalEntry> entries) =>
        ShowJournalAsync(new JournalPlayback("Bitácora", true, JournalSlides.ForLog(entries), 0));

    public Task JournalNextAsync() => MoveJournalAsync(+1);

    public Task JournalPreviousAsync() => MoveJournalAsync(-1);

    private async Task ShowJournalAsync(JournalPlayback playback)
    {
        if (playback.Slides.Count == 0) return;
        // La fecha de cada imagen decide su id: la lista se recarga antes de tomar el gate.
        await _content.PrepareAsync();
        await _gate.WaitAsync();
        try
        {
            if (Mode == TvMode.Off) return;
            var message = _content.BuildSlide(playback.Current);
            _showing = new ShownContent(null, null, null, playback.Title, _host.Current, playback);
            Mode = TvMode.Content;
            Send(message.Json, message.Files);
        }
        finally
        {
            _gate.Release();
        }
        Notify();
    }

    private async Task MoveJournalAsync(int step)
    {
        await _content.PrepareAsync();
        await _gate.WaitAsync();
        try
        {
            if (Mode != TvMode.Content || _showing is not { Journal: { } playback } shown) return;
            // El pase es una foto del momento: una imagen borrada o renombrada después se salta; si no queda ninguna, la TV vuelve al mapa.
            if (FindSlide(playback, step) is { } found)
            {
                _showing = shown with { Journal = playback with { Position = found.Position } };
                Send(found.Message.Json, found.Message.Files);
            }
            else if (!playback.Slides.Any(slide => IsShowable(slide, _content.BuildSlide(slide)))) LeaveContent();
            else return;
        }
        finally
        {
            _gate.Release();
        }
        Notify();
    }

    // Primera diapositiva con algo que mostrar a partir de la actual, en el sentido del paso.
    private (int Position, TvContentMessage Message)? FindSlide(JournalPlayback playback, int step)
    {
        for (var position = playback.Position + step; position >= 0 && position < playback.Slides.Count; position += step)
        {
            var message = _content.BuildSlide(playback.Slides[position]);
            if (IsShowable(playback.Slides[position], message)) return (position, message);
        }
        return null;
    }

    // Una página de nota no depende de ningún fichero; una imagen necesita seguir en el vault y entrar en la lista blanca.
    private bool IsShowable(JournalSlide slide, TvContentMessage message) =>
        slide.ImagePath is null || (message.Files.Count > 0 && _content.ImageExists(slide.ImagePath));
}
