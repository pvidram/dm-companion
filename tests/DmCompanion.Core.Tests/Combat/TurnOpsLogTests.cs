using DmCompanion.Core.Combat;
using DmCompanion.Core.Maps;

namespace DmCompanion.Core.Tests.Combat;

public class TurnOpsLogTests
{
    private static MapState WithLog()
    {
        var n = 0;
        var tokens = new[] { "t1", "t2", "t3" }.Select(id => new MapToken(id, id, null, TokenSide.Enemy, 1, 0, 0, 10, 10)).ToList();
        var s = TurnOps.Start(MapState.Empty with { Tokens = tokens }, [new("t1", 18), new("t2", 15), new("t3", 9)], () => $"e{++n}").State;
        var log = new[] { new CombatLogEntry("l1", 1, "Empezar combate", null, null, false, false) };
        return s with { Combat = s.Combat! with { Log = log } };
    }

    [Fact]
    public void RemoveEntry_keeps_the_log()
    {
        var s = TurnOps.RemoveEntry(WithLog(), "e2");
        s.Combat!.Log!.Select(e => e.Id).Should().Equal("l1");
    }

    [Fact]
    public void RemoveEntry_of_the_current_entry_keeps_the_log()
    {
        var s = TurnOps.RemoveEntry(WithLog(), "e1");
        s.Combat!.Log!.Select(e => e.Id).Should().Equal("l1");
    }

    [Fact]
    public void RemoveEntriesOf_keeps_the_log()
    {
        var s = TurnOps.RemoveEntriesOf(WithLog(), "t3");
        s.Combat!.Log!.Select(e => e.Id).Should().Equal("l1");
    }
}
