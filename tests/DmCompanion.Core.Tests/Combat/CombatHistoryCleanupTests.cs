using DmCompanion.Core.Combat;
using DmCompanion.Core.Maps;

namespace DmCompanion.Core.Tests.Combat;

/// <summary>Deshacer, rehacer y recuperar con tokens que ya no están en el mapa.</summary>
public class CombatHistoryCleanupTests
{
    private static Func<string> Ids(string prefix)
    {
        var n = 0;
        return () => $"{prefix}{++n}";
    }

    private static MapToken Tok(string id) => new(id, id, null, TokenSide.Enemy, 1, 0, 0, 10, 10);

    private static MapState Without(MapState s, string tokenId) => s with { Tokens = s.Tokens.Where(t => t.Id != tokenId).ToList() };

    // Combate con t1 (e1) → añadir t2 (e2) → quitar t1 del mapa. Deshacer volvería a un combate solo con t1.
    private static MapState AddedT2ThenT1Removed()
    {
        var ids = Ids("l");
        var started = CombatHistory.Start(MapState.Empty with { Tokens = [Tok("t1"), Tok("t2")] }, [new("t1", 18)], Ids("e")).State;
        var added = CombatHistory.Record(started, TurnOps.AddEntry(started, new("t2", 5), Ids("x")), "Añadir t2", ids);
        return Without(TurnOps.RemoveEntriesOf(added, "t1"), "t1");
    }

    [Fact]
    public void CanUndo_is_false_when_no_participant_of_the_copy_remains()
    {
        CombatHistory.CanUndo(AddedT2ThenT1Removed()).Should().BeFalse();
    }

    [Fact]
    public void Undo_returns_the_same_object_when_no_participant_of_the_copy_remains()
    {
        var s = AddedT2ThenT1Removed();
        CombatHistory.Undo(s).Should().BeSameAs(s);
    }

    [Fact]
    public void Undo_in_that_case_keeps_the_combat_and_its_log()
    {
        var s = AddedT2ThenT1Removed();
        CombatHistory.Undo(s).Combat!.Log.Should().HaveCount(2);
    }

    // Combate con t1 y t2; la acción A quita a t1 del combate; se deshace A; se quita t2 del mapa y se quita t1 del combate.
    private static MapState UndoneThenOnlyDeadParticipants()
    {
        var ids = Ids("l");
        var started = CombatHistory.Start(MapState.Empty with { Tokens = [Tok("t1"), Tok("t2")] }, [new("t1", 18), new("t2", 9)], Ids("e")).State;
        var acted = CombatHistory.Record(started, TurnOps.RemoveEntry(started, "e2"), "Sacar t2", ids);
        // Estado deshecho a mano: After = combate solo con t1.
        var undone = CombatHistory.Undo(acted);
        // Ahora el mapa pierde t1: el After (solo t1) ya no tiene participantes vivos.
        return Without(TurnOps.RemoveEntriesOf(undone, "t1"), "t1");
    }

    [Fact]
    public void CanRedo_is_false_when_no_participant_of_the_copy_remains()
    {
        CombatHistory.CanRedo(UndoneThenOnlyDeadParticipants()).Should().BeFalse();
    }

    [Fact]
    public void Redo_returns_the_same_object_when_no_participant_of_the_copy_remains()
    {
        var s = UndoneThenOnlyDeadParticipants();
        CombatHistory.Redo(s).Should().BeSameAs(s);
    }

    private static MapState EndedWithAllTokensGone()
    {
        var started = CombatHistory.Start(MapState.Empty with { Tokens = [Tok("t1")] }, [new("t1", 18)], Ids("e")).State;
        return Without(CombatHistory.End(started), "t1");
    }

    [Fact]
    public void CanRecover_is_false_when_no_participant_remains()
    {
        CombatHistory.CanRecover(EndedWithAllTokensGone()).Should().BeFalse();
    }

    [Fact]
    public void Recover_returns_the_same_object_when_no_participant_remains()
    {
        var s = EndedWithAllTokensGone();
        CombatHistory.Recover(s, Ids("l")).Should().BeSameAs(s);
    }

    [Fact]
    public void Recover_in_that_case_keeps_last_combat()
    {
        CombatHistory.Recover(EndedWithAllTokensGone(), Ids("l")).LastCombat.Should().NotBeNull();
    }

    // t1 (e1, anclado a sí mismo) y t2 (e2) con un estado de t1 anclado a e2; la acción quita a t2 del combate (reancla a e1... no: ancla de t1 a e2 → t1 propia entrada).
    private static MapState UndoWithAnchoredCondition()
    {
        var ids = Ids("l");
        var tokens = new[] { Tok("t1"), Tok("t2"), Tok("t3") };
        var started = CombatHistory.Start(MapState.Empty with { Tokens = tokens }, [new("t1", 18), new("t2", 12), new("t3", 5)], Ids("e")).State;
        // t3 tiene un estado de 3 rondas anclado al turno de t2 (e2).
        var anchored = ConditionOps.Add(started, "t3", new ConditionDraft("poisoned", "Envenenado", null, 3, "e2"), ids);
        var acted = CombatHistory.Record(anchored, TurnOps.RemoveEntry(anchored, "e3"), "Sacar t3", ids);
        // Deshacer devuelve a t3 al combate; si t2 se quitó del mapa, su entrada y las anclas a ella se limpian.
        return CombatHistory.Undo(Without(acted, "t2"));
    }

    [Fact]
    public void Undo_with_a_removed_token_drops_its_entry()
    {
        UndoWithAnchoredCondition().Combat!.Entries.Select(e => e.TokenId).Should().Equal("t1", "t3");
    }

    [Fact]
    public void Undo_with_a_removed_token_reanchors_conditions_of_other_tokens_to_their_own_entry()
    {
        var duration = UndoWithAnchoredCondition().Tokens.Single(t => t.Id == "t3").ConditionList.Single().Duration;
        duration!.EntryId.Should().Be(UndoWithAnchoredCondition().Combat!.Entries.Single(e => e.TokenId == "t3").Id);
    }

    [Fact]
    public void Undo_with_a_removed_token_drops_the_duration_when_the_owner_has_no_entry()
    {
        var ids = Ids("l");
        var started = CombatHistory.Start(MapState.Empty with { Tokens = [Tok("t1"), Tok("t2"), Tok("t3")] }, [new("t1", 18), new("t2", 12)], Ids("e")).State;
        // t3 no está en el combate; su estado está anclado a e2 (t2).
        var anchored = ConditionOps.Add(started, "t3", new ConditionDraft("poisoned", "Envenenado", null, 3, "e2"), ids);
        var acted = CombatHistory.Record(anchored, TurnOps.MoveEntry(anchored, "e1", 1), "Reordenar", ids);
        var undone = CombatHistory.Undo(Without(acted, "t2"));
        undone.Tokens.Single(t => t.Id == "t3").ConditionList.Single().Duration.Should().BeNull();
    }
}
