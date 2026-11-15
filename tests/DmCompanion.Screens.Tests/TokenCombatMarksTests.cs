using DmCompanion.Core.Combat;
using DmCompanion.Core.Maps;
using DmCompanion.Screens.Services;

namespace DmCompanion.Screens.Tests;

public sealed class TokenCombatMarksTests
{
    private static readonly MapToken Ana = new("a", "Ana", null, TokenSide.Pc, 1, 0, 0, null, 10);

    [Fact]
    public void Selected_is_marked_during_combat()
    {
        var state = MapState.Empty with
        {
            Tokens = [Ana],
            Combat = new CombatState(1, "e1", [new CombatEntry("e1", "a", 10)]),
        };

        TokenCombatMarks.For(state, Ana, "a").Selected.Should().BeTrue();
    }

    [Fact]
    public void Selected_is_not_marked_without_combat()
    {
        var state = MapState.Empty with { Tokens = [Ana] };

        TokenCombatMarks.For(state, Ana, "a").Selected.Should().BeFalse();
    }
}
