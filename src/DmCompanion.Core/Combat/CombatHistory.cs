using DmCompanion.Core.Maps;

namespace DmCompanion.Core.Combat;

/// <summary>
/// Historial del combate: apunta cada acción con una copia del estado de justo antes, y permite deshacer, rehacer,
/// terminar y recuperar. Funciones puras sobre <see cref="MapState"/>. Movimientos, trazos y datos del token que no
/// son de combate (posición, nombre, HpMax…) nunca se tocan.
/// </summary>
public static class CombatHistory
{
    public const int MaxEntries = 100;
    public const string StartCombatText = "Empezar combate";
    // Forma parte del formato persistido (.state.json): se usa para reconocer la entrada, no se puede cambiar sin migrar.
    public const string RecoveredCombatText = "Combate recuperado";

    public static CombatSnapshot Capture(MapState s) =>
        new(s.Combat is { } c ? c with { Log = null } : null, CaptureTokens(s));

    public static MapState Record(MapState before, MapState after, string text, Func<string> newId, bool undoable = true)
    {
        if (after.Combat is not { } combat || ReferenceEquals(before, after)) return after;
        var log = (combat.Log ?? []).Where(e => !e.Undone).ToList();
        log.Add(new CombatLogEntry(newId(), combat.Round, text, Capture(before), null, false, undoable));
        if (log.Count > MaxEntries) log.RemoveRange(0, log.Count - MaxEntries);
        return after with { Combat = combat with { Log = log } };
    }

    public static bool CanUndo(MapState s) =>
        LastDoneIndex(s) is var i && i >= 0 && s.Combat!.Log![i] is { Undoable: true, Before: { } before } && HasLiveEntry(s, before);

    public static bool CanRedo(MapState s) =>
        NextUndoneIndex(s) is var i && i >= 0 && s.Combat!.Log![i] is { After: { } after } && HasLiveEntry(s, after);

    public static MapState Undo(MapState s)
    {
        if (!CanUndo(s)) return s;
        var i = LastDoneIndex(s);
        var log = s.Combat!.Log!.ToList();
        var entry = log[i];
        log[i] = entry with { After = Capture(s), Undone = true };
        return ApplyAndClean(s, entry.Before!, log);
    }

    public static MapState Redo(MapState s)
    {
        if (!CanRedo(s)) return s;
        var i = NextUndoneIndex(s);
        var log = s.Combat!.Log!.ToList();
        var entry = log[i];
        log[i] = entry with { Undone = false };
        return ApplyAndClean(s, entry.After!, log);
    }

    /// <summary>Empieza un combate nuevo (sustituye al que hubiera), descarta LastCombat y apunta «Empezar combate» sin posibilidad de deshacer.</summary>
    public static CombatResult Start(MapState s, IReadOnlyList<InitiativeEntry> entries, Func<string> newId)
    {
        var fresh = s with { Combat = null };
        var result = TurnOps.Start(fresh, entries, newId);
        if (ReferenceEquals(result.State, fresh)) return CombatResult.Of(s);
        var started = result.State with { LastCombat = null };
        return result with { State = Record(fresh, started, StartCombatText, newId, undoable: false) };
    }

    /// <summary>Termina el combate guardándolo entero (con su historial) en LastCombat para poder recuperarlo.</summary>
    public static MapState End(MapState s) =>
        s.Combat is { } c ? TurnOps.End(s) with { LastCombat = new CombatSnapshot(c, CaptureTokens(s)) } : s;

    public static bool CanRecover(MapState s) => s.Combat is null && s.LastCombat is { } last && HasLiveEntry(s, last);

    public static MapState Recover(MapState s, Func<string> newId)
    {
        if (!CanRecover(s)) return s;
        var last = s.LastCombat!;
        var saved = last.Combat!;
        var restored = Clean(ApplyFields(s with { Combat = saved, LastCombat = null }, last.Tokens));
        return Record(s, restored, RecoveredCombatText, newId, undoable: false);
    }

    // Aplicar una copia sin ningún participante cuyo token siga en el mapa dejaría el combate vacío (y lo terminaría): no se aplica.
    private static bool HasLiveEntry(MapState s, CombatSnapshot snapshot) =>
        snapshot.Combat is { } c && c.Entries.Any(e => s.Tokens.Any(t => t.Id == e.TokenId));

    // La entrada «Combate recuperado» es transparente: deshacer y rehacer la saltan y actúan sobre la última acción real.
    private static int LastDoneIndex(MapState s) =>
        s.Combat?.Log is { } log ? log.ToList().FindLastIndex(e => !e.Undone && e is not { Undoable: false, Text: RecoveredCombatText }) : -1;

    // Primera entrada deshecha tras la última hecha; salta «Combate recuperado» (que nunca está deshecha) para ser coherente con Undo.
    private static int NextUndoneIndex(MapState s) =>
        s.Combat?.Log is { } log ? log.ToList().FindIndex(LastDoneIndex(s) + 1, e => e.Undone) : -1;

    private static List<TokenCombatFields> CaptureTokens(MapState s) =>
        s.Tokens.Select(t => new TokenCombatFields(t.Id, t.HpCurrent, t.HpTemp, t.Conditions, t.DeathSaves, t.Dead)).ToList();

    // Sustituye el combate (conservando el historial indicado) y los campos de combate de los tokens que sigan en el mapa.
    private static MapState ApplyAndClean(MapState s, CombatSnapshot snapshot, IReadOnlyList<CombatLogEntry> log)
    {
        var applied = ApplyFields(s with { Combat = snapshot.Combat! with { Log = log } }, snapshot.Tokens);
        return Clean(applied);
    }

    private static MapState ApplyFields(MapState s, IReadOnlyList<TokenCombatFields> fields)
    {
        var byId = new Dictionary<string, TokenCombatFields>();
        foreach (var f in fields) byId.TryAdd(f.TokenId, f);
        var tokens = s.Tokens.Select(t => byId.TryGetValue(t.Id, out var f)
            ? t with { HpCurrent = f.HpCurrent, HpTemp = f.HpTemp, Conditions = f.Conditions, DeathSaves = f.DeathSaves, Dead = f.Dead }
            : t).ToList();
        return s with { Tokens = tokens };
    }

    // Quita las entradas de combate de tokens que ya no existen (HasLiveEntry garantiza que queda alguna).
    private static MapState Clean(MapState s)
    {
        if (s.Combat is not { } combat) return s;
        var ids = s.Tokens.Select(t => t.Id).ToHashSet();
        foreach (var orphan in combat.Entries.Select(e => e.TokenId).Where(id => !ids.Contains(id)).Distinct().ToList())
            s = TurnOps.RemoveEntriesOf(s, orphan);
        return s;
    }
}
