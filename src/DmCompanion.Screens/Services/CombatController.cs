using DmCompanion.Core.Combat;
using DmCompanion.Core.Maps;
using DmCompanion.Screens.Platform;

namespace DmCompanion.Screens.Services;

/// <summary>Operaciones de combate sobre el mapa abierto. Cada una pasa por <see cref="MapSession.Mutate"/>: se guarda y se recarga como cualquier otro cambio.</summary>
public sealed class CombatController
{
    private const string StripKey = "combat.strip.expanded";
    private static readonly TimeSpan EndedNoticeLifetime = TimeSpan.FromSeconds(10);

    private readonly MapSession _session;
    private readonly TokenHitPoints _hp;
    private readonly IAppPreferences _prefs;
    private readonly List<CombatNotice> _notices = [];
    private readonly object _noticeLock = new();
    private readonly TimeProvider _time;
    private DateTimeOffset? _endedUntil;
    private ITimer? _endedTimer;
    private string? _mapPath;

    public CombatController(MapSession session, TokenHitPoints hp, IAppPreferences prefs, TimeProvider time)
    {
        (_session, _hp, _prefs, _time) = (session, hp, prefs, time);
        _mapPath = session.Current?.NotePath;
        session.Changed += OnSessionChanged;
    }

    public event EventHandler? Changed;

    public bool IsActive => State is not null;
    public bool CanEdit => _session.Editor is not null && !_session.IsReadOnly;
    public CombatState? State => _session.Editor?.State.Combat;

    public IReadOnlyList<CombatNotice> Notices
    {
        get
        {
            lock (_noticeLock)
            {
                DropExpiredEndedNotice();
                return [.. _notices];
            }
        }
    }

    public IReadOnlyList<CombatLogEntry> Log => State?.Log ?? [];
    public bool CanUndo => CanEdit && _session.Editor is { } e && CombatHistory.CanUndo(e.State);
    public bool CanRedo => CanEdit && _session.Editor is { } e && CombatHistory.CanRedo(e.State);
    public bool CanRecover => CanEdit && _session.Editor is { } e && CombatHistory.CanRecover(e.State);

    public bool StripExpanded
    {
        get => _prefs.Get(StripKey) != "false";
        set
        {
            if (StripExpanded == value) return;
            _prefs.Set(StripKey, value ? "true" : "false");
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Start(IReadOnlyList<InitiativeEntry> entries)
    {
        if (!CanEdit) return;
        var cleared = ClearNotices();
        if (Apply(s => CombatHistory.Start(s, entries, NewId))) StripExpanded = true;
        else if (cleared) Changed?.Invoke(this, EventArgs.Empty);
    }

    public void AddEntry(InitiativeEntry entry) =>
        Apply(s => TurnOps.AddEntry(s, entry, NewId), (_, after) => CombatLogText.EntryAdded(after, entry.TokenId));

    public void MoveEntry(string entryId, int newIndex) =>
        Apply(s => TurnOps.MoveEntry(s, entryId, newIndex), (before, _) => CombatLogText.EntryMoved(before, entryId));

    public void RemoveEntry(string entryId) => Apply(s => EndIfEmptied(s, TurnOps.RemoveEntry(s, entryId)), (before, _) => CombatLogText.EntryRemoved(before, entryId));

    public void NextTurn() => Apply(TurnOps.NextTurn, (before, after) => CombatLogText.Turn(before, after));

    public void SetCurrent(string entryId) =>
        Apply(s => TurnOps.SetCurrent(s, entryId), (before, after) => CombatLogText.Turn(before, after, byHand: true));

    public void End()
    {
        var cleared = CanEdit && ClearNotices();
        if (!Apply(s => s.Combat is null ? CombatResult.Of(s) : new CombatResult(CombatHistory.End(s), [new CombatEnded()])) && cleared)
            Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Undo() => Replay(CombatHistory.Undo);
    public void Redo() => Replay(CombatHistory.Redo);
    public void Recover() => Replay(s => CombatHistory.Recover(s, NewId));

    public void AddCondition(string tokenId, ConditionDraft draft) =>
        Apply(s => ConditionOps.Add(s, tokenId, draft, NewId), (_, after) => CombatLogText.ConditionAdded(after, tokenId, draft));

    public void UpdateCondition(string tokenId, string conditionId, ConditionDraft draft) =>
        Apply(s => ConditionOps.Update(s, tokenId, conditionId, draft), (_, after) => CombatLogText.ConditionEdited(after, tokenId, draft));

    public void RemoveCondition(string tokenId, string conditionId) =>
        Apply(s => ConditionOps.Remove(s, tokenId, conditionId), (before, _) => CombatLogText.ConditionRemoved(before, tokenId, conditionId));

    public void KeepExpired(string tokenId, string conditionId, int? rounds) =>
        Apply(s => ConditionOps.KeepExpired(s, tokenId, conditionId, rounds), (before, _) => CombatLogText.ConditionKept(before, tokenId, conditionId, rounds));

    public void Damage(string tokenId, int amount) =>
        Apply(s => HitPointOps.Damage(s, tokenId, amount, MaxOf(s, tokenId), _hp.Rules),
            (_, after) => CombatLogText.Damage(after, tokenId, amount, MaxOf(after, tokenId)));

    public void Heal(string tokenId, int amount) =>
        Apply(s => HitPointOps.Heal(s, tokenId, amount, MaxOf(s, tokenId), _hp.Rules),
            (before, after) => CombatLogText.Heal(before, after, tokenId, amount, MaxOf(after, tokenId)));

    public void SetTempHp(string tokenId, int value) =>
        Apply(s => HitPointOps.SetTempHp(s, tokenId, value), (_, after) => CombatLogText.TempHp(after, tokenId, value));

    public void SetDeathSaves(string tokenId, int successes, int failures) =>
        Apply(s => HitPointOps.SetDeathSaves(s, tokenId, successes, failures), (_, after) => CombatLogText.DeathSaves(after, tokenId));

    /// <summary>Fija los HP actuales (recortados a [0, máximo]); con combate activo queda en el historial.</summary>
    public void SetHp(string tokenId, int value) =>
        Apply(s => MaxOf(s, tokenId) is { } max ? HitPointOps.SetHp(s, tokenId, value, max) : s,
            (before, after) => CombatLogText.SetHp(before, after, tokenId, MaxOf(after, tokenId)));

    /// <summary>Interpreta lo escrito en los HP («12», «-5», «+3») sobre el estado actual, dentro de la mutación. Devuelve false si el texto no es válido.</summary>
    public bool SetHpFromText(string tokenId, string text)
    {
        var valid = true;
        Apply(s =>
        {
            if (s.Tokens.FirstOrDefault(t => t.Id == tokenId) is not { } token || MaxOf(s, tokenId) is not { } max) return s;
            var entry = HpEntry.Apply(Math.Min(token.HpCurrent ?? max, max), max, text);
            valid = entry.Ok;
            return entry.Ok ? HitPointOps.SetHp(s, tokenId, entry.Value, max) : s;
        }, (before, after) => CombatLogText.SetHp(before, after, tokenId, MaxOf(after, tokenId)));
        return valid;
    }

    public void MarkDead(string tokenId) => Apply(s => DeathOps.MarkDead(s, tokenId), (_, after) => CombatLogText.Dead(after, tokenId));
    public void Revive(string tokenId) => Apply(s => DeathOps.Revive(s, tokenId), (_, after) => CombatLogText.Revive(after, tokenId));

    public void Dismiss(CombatNotice notice)
    {
        bool removed;
        lock (_noticeLock) removed = _notices.Remove(notice);
        if (removed) Changed?.Invoke(this, EventArgs.Empty);
    }

    // Los avisos son de un combate: no pasan al siguiente.
    private bool ClearNotices()
    {
        lock (_noticeLock)
        {
            DropExpiredEndedNotice();
            if (_notices.Count == 0) return false;
            _notices.Clear();
            StopEndedTimer();
            return true;
        }
    }

    private static string NewId() => MapSession.NewId();

    private int? MaxOf(MapState s, string tokenId) =>
        s.Tokens.FirstOrDefault(t => t.Id == tokenId) is { } token ? _hp.MaxFor(token) : null;

    // Deshacer, rehacer y recuperar: no se apuntan y vacían los avisos (pueden no corresponder ya al estado restaurado).
    private void Replay(Func<MapState, MapState> change)
    {
        if (!CanEdit) return;
        var cleared = ClearNotices();
        if (!Apply(s => CombatResult.Of(change(s)), suggestDead: false) && cleared) Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Apply(Func<MapState, MapState> change, Func<MapState, MapState, string> text) =>
        Apply(s => CombatResult.Of(change(s)), text);

    private void Apply(Func<MapState, CombatResult> change, Func<MapState, MapState, string> text) =>
        Apply(s => Recorded(s, change(s), text));

    // Con combate activo apunta la acción en el historial; si no cambió nada de combate, devuelve el mismo estado (no se guarda ni se apunta).
    private CombatResult Recorded(MapState before, CombatResult result, Func<MapState, MapState, string> text)
    {
        if (before.Combat is null) return result;
        if (!CombatChange.Differs(before, result.State)) return CombatResult.Of(before);
        return result with { State = CombatHistory.Record(before, result.State, text(before, result.State), NewId) };
    }

    // Quitar la última entrada termina el combate: se guarda para poder recuperarlo, como «Terminar combate».
    private static CombatResult EndIfEmptied(MapState before, MapState after) =>
        before.Combat is not null && after.Combat is null
            ? new CombatResult(after with { LastCombat = CombatHistory.End(before).LastCombat }, [new CombatEnded()])
            : CombatResult.Of(after);

    private bool Apply(Func<MapState, MapState> change) => Apply(s => CombatResult.Of(change(s)));

    private bool Apply(Func<MapState, CombatResult> change, bool suggestDead = true)
    {
        if (!CanEdit) return false;
        List<CombatNotice> raised = [];
        var changed = false;
        _session.Mutate(editor => changed = editor.ReplaceState(s =>
        {
            var result = change(s);
            raised = [.. result.Notices];
            if (suggestDead) raised.AddRange(CombatChange.NewThreeFailures(s, result.State).Select(id => new ThreeFailures(id)));
            return result.State;
        }));
        if (!changed) return false;
        AddNotices(raised);
        // MapSession.Changed ya avisó antes de añadir los avisos: se vuelve a avisar con ellos presentes.
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    // El aviso de «Combate terminado» desaparece con la siguiente acción; el nuevo, si lo hay, caduca a los 10 s.
    private void AddNotices(IReadOnlyList<CombatNotice> raised)
    {
        lock (_noticeLock)
        {
            _notices.RemoveAll(n => n is CombatEnded);
            StopEndedTimer();
            _notices.AddRange(raised);
            if (!raised.Any(n => n is CombatEnded)) return;
            _endedUntil = _time.GetUtcNow() + EndedNoticeLifetime;
            // El temporizador solo avisa a la UI de que el aviso ya no está: Notices lo filtra por hora.
            _endedTimer = _time.CreateTimer(_ => Changed?.Invoke(this, EventArgs.Empty), null, EndedNoticeLifetime, Timeout.InfiniteTimeSpan);
        }
    }

    private void DropExpiredEndedNotice()
    {
        if (_endedUntil is not { } until || _time.GetUtcNow() < until) return;
        _notices.RemoveAll(n => n is CombatEnded);
        StopEndedTimer();
    }

    private void StopEndedTimer()
    {
        _endedTimer?.Dispose();
        (_endedTimer, _endedUntil) = (null, null);
    }

    private void OnSessionChanged(object? sender, EventArgs e)
    {
        var path = _session.Current?.NotePath;
        bool cleared;
        lock (_noticeLock)
        {
            if (path == _mapPath) cleared = false;
            else (_mapPath, cleared) = (path, _notices.Count > 0);
            if (cleared) { _notices.Clear(); StopEndedTimer(); }
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
