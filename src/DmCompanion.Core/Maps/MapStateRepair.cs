using DmCompanion.Core.Combat;

namespace DmCompanion.Core.Maps;

/// <summary>Corrige los datos de combate incoherentes (el fichero se edita a mano y se sincroniza): se arreglan, no invalidan el estado.</summary>
public static class MapStateRepair
{
    public const int MaxLogEntries = 100;

    public static (MapState State, int Repairs) Repair(MapState state)
    {
        var repairs = 0;
        var combat = RepairCombat(state.Combat, state.Tokens, ref repairs);
        var entries = combat?.Entries ?? [];
        var tokens = new List<MapToken>();
        foreach (var token in state.Tokens)
        {
            tokens.Add(RepairToken(token, entries, ref repairs));
        }

        var lastCombat = RepairLastCombat(state.LastCombat, ref repairs);
        return (state with { Combat = combat, Tokens = tokens, LastCombat = lastCombat }, repairs);
    }

    /// <summary>
    /// Un último combate sin combate válido o sin lista de tokens no sirve para recuperarlo: se descarta entero.
    /// Que sus participantes ya no estén en el mapa es legítimo (se borran tras terminar): no se poda ni cuenta como reparación,
    /// la recuperación ya lo resuelve.
    /// </summary>
    private static CombatSnapshot? RepairLastCombat(CombatSnapshot? last, ref int repairs)
    {
        if (last is null)
        {
            return null;
        }

        if (last.Tokens is null || last.Combat is null)
        {
            repairs++;
            return null;
        }

        var combat = RepairCombat(last.Combat, null, ref repairs);
        return combat is null ? null : new CombatSnapshot(combat, RepairSnapshotTokens(last.Tokens, ref repairs));
    }

    /// <summary>
    /// Para el estado de versión futura, que solo se lee: listas nulas pasan a vacías y se quitan los elementos nulos o sin id o texto,
    /// para que la interfaz no se rompa. No cuenta como reparación ni se escribe: el mapa sigue en solo lectura.
    /// </summary>
    public static MapState Normalize(MapState state)
    {
        var combat = state.Combat is { } c ? c with { Entries = [.. (c.Entries ?? []).Where(e => e is not null)] } : null;
        var tokens = state.Tokens.Select(t => t.Conditions is null
            ? t
            : t with { Conditions = [.. t.Conditions.Where(IsComplete)] }).ToList();
        return state with { Combat = combat, Tokens = tokens };
    }

    private static bool IsComplete(TokenCondition? condition) => condition is { Id: not null, Label: not null };

    private static CombatState? RepairCombat(CombatState? combat, IReadOnlyList<MapToken>? tokens, ref int repairs)
    {
        if (combat is null)
        {
            return null;
        }

        // tokens null: no se comprueba que los participantes sigan en el mapa (último combate).
        var tokenIds = tokens?.Select(t => t.Id).ToHashSet();
        var seen = new HashSet<string>();
        var entries = new List<CombatEntry>();
        foreach (var entry in combat.Entries ?? [])
        {
            if (entry is null || (tokenIds is not null && !tokenIds.Contains(entry.TokenId)) || !seen.Add(entry.Id))
            {
                repairs++;
                continue;
            }

            entries.Add(entry);
        }

        if (entries.Count == 0)
        {
            repairs++;
            return null;
        }

        var round = combat.Round;
        if (round < 1)
        {
            round = 1;
            repairs++;
        }

        var current = combat.CurrentEntryId;
        if (current is not null && !seen.Contains(current))
        {
            current = entries[0].Id;
            repairs++;
        }

        return new CombatState(round, current, entries, RepairLog(combat.Log, ref repairs));
    }

    private static IReadOnlyList<CombatLogEntry>? RepairLog(IReadOnlyList<CombatLogEntry>? log, ref int repairs)
    {
        if (log is null)
        {
            return null;
        }

        var seen = new HashSet<string>();
        var kept = new List<CombatLogEntry>();
        foreach (var entry in log)
        {
            if (entry is null || entry.Id is null || entry.Text is null || !seen.Add(entry.Id) || (entry.Before is null && entry.Undoable))
            {
                repairs++;
                continue;
            }

            kept.Add(entry with { Before = RepairSnapshot(entry.Before, ref repairs), After = RepairSnapshot(entry.After, ref repairs) });
        }

        if (kept.Count > MaxLogEntries)
        {
            repairs += kept.Count - MaxLogEntries;
            kept = kept[^MaxLogEntries..];
        }

        return kept;
    }

    /// <summary>Copia del historial: sin Log (no anida historiales), sin elementos nulos en entradas ni tokens. Sin reparación profunda del combate.</summary>
    private static CombatSnapshot? RepairSnapshot(CombatSnapshot? snapshot, ref int repairs)
    {
        if (snapshot is null)
        {
            return null;
        }

        var combat = snapshot.Combat;
        if (combat is not null)
        {
            var entries = combat.Entries ?? [];
            if (combat.Entries is null || combat.Log is not null || entries.Any(e => e is null))
            {
                repairs++;
            }

            combat = combat with { Entries = [.. entries.Where(e => e is not null)], Log = null };
        }

        return new CombatSnapshot(combat, RepairSnapshotTokens(snapshot.Tokens, ref repairs));
    }

    /// <summary>Regla única para la lista de tokens de cualquier copia (historial y último combate): null pasa a vacía y se quitan los elementos nulos o sin id.</summary>
    private static IReadOnlyList<TokenCombatFields> RepairSnapshotTokens(IReadOnlyList<TokenCombatFields>? tokens, ref int repairs)
    {
        if (tokens is null)
        {
            repairs++;
            return [];
        }

        var kept = tokens.Where(t => t is { TokenId: not null }).ToList();
        repairs += tokens.Count - kept.Count;
        return kept;
    }

    private static MapToken RepairToken(MapToken token, IReadOnlyList<CombatEntry> entries, ref int repairs)
    {
        var hpTemp = token.HpTemp;
        if (hpTemp < 0)
        {
            hpTemp = 0;
            repairs++;
        }

        var saves = token.DeathSaves;
        if (saves is not null)
        {
            var clamped = new DeathSaves(Math.Clamp(saves.Successes, 0, 3), Math.Clamp(saves.Failures, 0, 3));
            if (clamped != saves)
            {
                repairs++;
            }

            saves = clamped;
        }

        List<TokenCondition>? conditions = null;
        if (token.Conditions is not null)
        {
            conditions = [];
            foreach (var condition in token.Conditions)
            {
                if (!IsComplete(condition))
                {
                    repairs++;
                    continue;
                }

                conditions.Add(RepairCondition(condition!, token.Id, entries, ref repairs));
            }
        }

        return token with { HpTemp = hpTemp, DeathSaves = saves, Conditions = conditions };
    }

    private static TokenCondition RepairCondition(TokenCondition condition, string tokenId, IReadOnlyList<CombatEntry> entries, ref int repairs)
    {
        var duration = condition.Duration;
        if (duration is null)
        {
            return condition;
        }

        if (entries.All(e => e.Id != duration.EntryId))
        {
            // Reancla al turno del propio token; si no participa en el combate, la duración se pierde.
            var own = entries.FirstOrDefault(e => e.TokenId == tokenId);
            duration = own is null ? null : duration with { EntryId = own.Id };
            repairs++;
        }

        if (duration is { TurnsLeft: < 0 })
        {
            duration = duration with { TurnsLeft = 0 };
            repairs++;
        }

        return condition with { Duration = duration };
    }
}
