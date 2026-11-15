namespace DmCompanion.Core.Audio;

/// <summary>Orden aleatorio sin repetir hasta agotar la vuelta; la vuelta nueva no empieza por el último track de la anterior.</summary>
public sealed class ShufflePlaylist(IReadOnlyList<string> tracks, Random random)
{
    private readonly Queue<string> _round = new();
    private string? _last;

    public string? Next()
    {
        if (tracks.Count == 0)
            return null;

        if (_round.Count == 0)
            StartRound();

        return _last = _round.Dequeue();
    }

    private void StartRound()
    {
        var order = tracks.OrderBy(_ => random.Next()).ToList();
        if (order.Count > 1 && order[0] == _last)
        {
            var swapWith = random.Next(1, order.Count);
            (order[0], order[swapWith]) = (order[swapWith], order[0]);
        }

        foreach (var track in order)
            _round.Enqueue(track);
    }
}
