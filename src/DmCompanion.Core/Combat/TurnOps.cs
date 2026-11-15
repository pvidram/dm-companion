using DmCompanion.Core.Maps;

namespace DmCompanion.Core.Combat;

/// <summary>
/// Turnos y rondas del combate. Funciones puras sobre <see cref="MapState"/>; sin combate activo
/// devuelven el mismo estado. El descuento de duraciones ocurre al empezar el turno de la entrada ancla.
/// </summary>
public static class TurnOps
{
    public static CombatResult Start(MapState s, IReadOnlyList<InitiativeEntry> entries, Func<string> newId)
    {
        if (entries.Count == 0) return CombatResult.Of(s);
        var created = entries.Select(e => new CombatEntry(newId(), e.TokenId, e.Initiative)).ToList();
        // OrderBy es estable: los empates conservan el orden en que se apuntaron.
        var ordered = created.OrderBy(e => e.Initiative is null).ThenByDescending(e => e.Initiative ?? 0).ToList();
        var first = ordered.FirstOrDefault(e => !IsDead(s, e)) ?? ordered[0];
        var combat = new CombatState(1, first.Id, ordered);
        return StartTurn(s with { Combat = combat }, first.Id);
    }

    public static MapState AddEntry(MapState s, InitiativeEntry entry, Func<string> newId)
    {
        if (s.Combat is not { } c) return s;
        var created = new CombatEntry(newId(), entry.TokenId, entry.Initiative);
        var index = c.Entries.Count;
        if (entry.Initiative is { } n)
        {
            var after = c.Entries.ToList().FindLastIndex(e => e.Initiative >= n);
            index = after >= 0 ? after + 1 : 0;
        }
        var list = c.Entries.ToList();
        list.Insert(index, created);
        return s with { Combat = c with { Entries = list } };
    }

    public static MapState MoveEntry(MapState s, string entryId, int newIndex)
    {
        if (s.Combat is not { } c) return s;
        var list = c.Entries.ToList();
        var from = list.FindIndex(e => e.Id == entryId);
        if (from < 0) return s;
        var entry = list[from];
        list.RemoveAt(from);
        list.Insert(Math.Clamp(newIndex, 0, list.Count), entry);
        return s with { Combat = c with { Entries = list } };
    }

    public static MapState RemoveEntry(MapState s, string entryId)
    {
        if (s.Combat is not { } c) return s;
        var index = c.Entries.ToList().FindIndex(e => e.Id == entryId);
        if (index < 0) return s;
        var removed = c.Entries[index];
        var rest = c.Entries.Where(e => e.Id != entryId).ToList();
        if (rest.Count == 0) return End(s);

        var round = c.Round;
        var current = c.CurrentEntryId;
        if (current == entryId)
        {
            // El turno pasa a la siguiente sin descuentos; si era la última, empieza la ronda siguiente.
            if (index >= rest.Count) { current = rest[0].Id; round++; }
            else current = rest[index].Id;
        }
        var tokens = s.Tokens.Select(t => Reanchor(t, removed.Id, rest)).ToList();
        return s with { Tokens = tokens, Combat = c with { Round = round, CurrentEntryId = current, Entries = rest } };
    }

    public static MapState RemoveEntriesOf(MapState s, string tokenId)
    {
        if (s.Combat is not { } c) return s;
        foreach (var entry in c.Entries.Where(e => e.TokenId == tokenId))
            s = RemoveEntry(s, entry.Id);
        return s;
    }

    public static CombatResult NextTurn(MapState s)
    {
        if (s.Combat is not { Entries.Count: > 0 } c) return CombatResult.Of(s);
        var index = c.Entries.ToList().FindIndex(e => e.Id == c.CurrentEntryId);
        // Salta las entradas muertas sin empezar su turno. Si todas lo están, no cambia nada.
        for (var step = 1; step <= c.Entries.Count; step++)
        {
            var next = (index + step) % c.Entries.Count;
            if (index < 0) next = step - 1;
            var entry = c.Entries[next];
            if (IsDead(s, entry)) continue;
            var wrapped = index >= 0 && index + step >= c.Entries.Count;
            var round = wrapped ? c.Round + 1 : c.Round;
            return StartTurn(s with { Combat = c with { Round = round, CurrentEntryId = entry.Id } }, entry.Id);
        }
        return CombatResult.Of(s);
    }

    // Sin token en el mapa la entrada se trata como viva.
    private static bool IsDead(MapState s, CombatEntry e) =>
        s.Tokens.FirstOrDefault(t => t.Id == e.TokenId)?.IsDead == true;

    public static MapState SetCurrent(MapState s, string entryId)
    {
        if (s.Combat is not { } c || c.Entries.All(e => e.Id != entryId)) return s;
        return s with { Combat = c with { CurrentEntryId = entryId } };
    }

    public static MapState End(MapState s)
    {
        if (s.Combat is null) return s;
        var tokens = s.Tokens.Select(t => t with
        {
            DeathSaves = null,
            Conditions = t.Conditions?.Select(x => x with { Duration = null, Expired = false }).ToList(),
        }).ToList();
        return s with { Tokens = tokens, Combat = null };
    }

    // Descuenta al empezar el turno de entryId; los estados vencidos no se tocan.
    private static CombatResult StartTurn(MapState s, string entryId)
    {
        var notices = new List<CombatNotice>();
        var tokens = s.Tokens.Select(t =>
        {
            if (!t.ConditionList.Any(x => !x.Expired && x.Duration?.EntryId == entryId)) return t;
            var conditions = t.ConditionList.Select(x =>
            {
                if (x.Expired || x.Duration?.EntryId != entryId) return x;
                var left = x.Duration.TurnsLeft - 1;
                if (left > 0) return x with { Duration = x.Duration with { TurnsLeft = left } };
                notices.Add(new ConditionExpired(t.Id, x.Id));
                return x with { Duration = x.Duration with { TurnsLeft = 0 }, Expired = true };
            }).ToList();
            return t with { Conditions = conditions };
        }).ToList();
        return new CombatResult(s with { Tokens = tokens }, notices);
    }

    // Reancla a la entrada del propio token afectado; sin ella, la duración se pierde.
    private static MapToken Reanchor(MapToken t, string removedEntryId, IReadOnlyList<CombatEntry> rest)
    {
        if (!t.ConditionList.Any(x => x.Duration?.EntryId == removedEntryId)) return t;
        var own = rest.FirstOrDefault(e => e.TokenId == t.Id);
        var conditions = t.ConditionList.Select(x =>
            x.Duration?.EntryId != removedEntryId ? x
            : own is null ? x with { Duration = null }
            : x with { Duration = x.Duration with { EntryId = own.Id } }).ToList();
        return t with { Conditions = conditions };
    }
}
