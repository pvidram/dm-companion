using DmCompanion.Core.Combat;
using DmCompanion.Core.Maps;

namespace DmCompanion.Core.Tests;

public class MapEditorCombatTests
{
    private static (MapEditor Editor, string A, string B) InCombat()
    {
        var n = 0;
        var ed = new MapEditor(MapState.Empty, 1000, 500, () => $"id{++n}");
        var draft = new TokenDraft("Goblin", null, TokenSide.Enemy, 1, 15, 7);
        var a = ed.AddToken(draft, 1, 1);
        var b = ed.AddToken(draft, 2, 2);
        ed.ReplaceState(s => CombatHistory.Start(s, [new(a.Id, 18), new(b.Id, 9)], () => $"c{++n}").State);
        return (ed, a.Id, b.Id);
    }

    [Fact]
    public void RemoveToken_in_combat_keeps_the_log()
    {
        var (ed, a, _) = InCombat();
        ed.RemoveToken(a);
        ed.State.Combat!.Log!.Select(e => e.Text).Should().Equal(CombatHistory.StartCombatText);
    }

    [Fact]
    public void RemoveToken_of_the_last_participant_ends_the_combat()
    {
        var (ed, a, b) = InCombat();
        ed.RemoveToken(a);
        ed.RemoveToken(b);
        ed.State.Combat.Should().BeNull();
    }

    [Fact]
    public void RemoveToken_of_the_last_participant_does_not_set_last_combat()
    {
        var (ed, a, b) = InCombat();
        ed.RemoveToken(a);
        ed.RemoveToken(b);
        ed.State.LastCombat.Should().BeNull();
    }

    [Fact]
    public void End_then_removing_every_participant_round_trips_without_repairs()
    {
        var (ed, a, b) = InCombat();
        ed.ReplaceState(CombatHistory.End);
        ed.RemoveToken(a);
        ed.RemoveToken(b);

        var read = MapStateSerializer.Read(MapStateSerializer.Write(ed.State));

        read.Repairs.Should().Be(0);
    }

    [Fact]
    public void RemoveToken_with_participants_left_does_not_set_last_combat()
    {
        var (ed, a, _) = InCombat();
        ed.RemoveToken(a);
        ed.State.LastCombat.Should().BeNull();
    }
}
