using DmCompanion.Core.Combat;
using DmCompanion.Core.GameSystems;
using DmCompanion.Core.Maps;
using DmCompanion.Core.Tv;

namespace DmCompanion.Core.Tests.Tv;

public class PlayerViewProjectorTests
{
    private static readonly MapDefinition Map = new("mapas/cueva.md", "Cueva", "mapas/cueva.png", 2000, 1000);
    private static readonly TvFrame Frame = new(0, 0, 1600, 900);

    private static MapToken Tok(string id, TokenSide side = TokenSide.Enemy, int? hpCurrent = null, int? temp = null,
        bool hidden = false, string? alias = null, bool dead = false, params TokenCondition[] conditions) =>
        new(id, $"Nombre {id}", null, side, 1, 10, 20, null, null, hpCurrent, temp,
            conditions.Length == 0 ? null : conditions, null, dead ? true : null, hidden ? true : null, alias);

    private static MapState State(IReadOnlyList<MapToken> tokens, CombatState? combat = null) =>
        MapState.Empty with { Tokens = tokens, Combat = combat };

    private static CombatState Combat(string current, params string[] tokenIds) =>
        new(3, "e-" + current, tokenIds.Select((t, i) => new CombatEntry("e-" + t, t, 20 - i)).ToList());

    private static PlayerMapView Project(MapState s, int? max = 20, ICombatRules? rules = null) =>
        PlayerViewProjector.Project(Map, s, Frame, rules ?? Dnd5eCombatRules.Instance, _ => max,
            t => new TokenLook($"retratos/{t.Id}.png"));

    private static string? Hp(MapToken t, int? max = 20) =>
        Project(State([t], Combat(t.Id, t.Id)), max).Combat!.Entries.Single().Hp;

    [Fact]
    public void Hidden_token_is_absent_from_tokens_and_strip()
    {
        var cond = new TokenCondition("c1", "stunned", "Aturdido", null, null, false);
        var hidden = Tok("h", hidden: true, dead: true, conditions: cond);
        var view = Project(State([hidden, Tok("a")], Combat("h", "h", "a")));
        view.Tokens.Select(t => t.Id).Should().Equal("a");
        view.Combat!.Entries.Select(e => e.TokenId).Should().Equal("a");
        view.Combat.Entries.Any(e => e.Current).Should().BeFalse();
        view.Combat.Round.Should().Be(3);
    }

    [Fact]
    public void Alias_replaces_name_in_tokens_and_strip()
    {
        var view = Project(State([Tok("a", alias: "Sombra")], Combat("a", "a")));
        (view.Tokens.Single().Name, view.Combat!.Entries.Single().Name).Should().Be(("Sombra", "Sombra"));
    }

    [Fact]
    public void Pc_hp_is_exact() => Hp(Tok("p", TokenSide.Pc, 12)).Should().Be("12/20");

    [Fact]
    public void Pc_hp_shows_temp() => Hp(Tok("p", TokenSide.Pc, 12, 3)).Should().Be("12/20 (+3)");

    [Fact]
    public void Pc_hp_without_max_shows_current_only() => Hp(Tok("p", TokenSide.Pc, 12), null).Should().Be("12");

    [Theory]
    [InlineData(20, "Ileso")]
    [InlineData(11, "Herido")]
    [InlineData(10, "Malherido")]
    [InlineData(1, "Malherido")]
    [InlineData(0, null)]
    public void Enemy_hp_is_a_label(int hp, string? expected) => Hp(Tok("e", TokenSide.Enemy, hp)).Should().Be(expected);

    [Fact]
    public void Ally_hp_is_a_label() => Hp(Tok("a", TokenSide.Ally, 11)).Should().Be("Herido");

    [Fact]
    public void Enemy_without_max_has_no_hp() => Hp(Tok("e", TokenSide.Enemy, 5), null).Should().BeNull();

    [Fact]
    public void Dead_and_conditions_are_visible()
    {
        var cond = new TokenCondition("c1", "stunned", "Aturdido", null, null, false);
        var view = Project(State([Tok("e", hpCurrent: 0, dead: true, conditions: cond)], Combat("e", "e")));
        (view.Tokens.Single().Dead, view.Tokens.Single().Down, view.Tokens.Single().Conditions, view.Combat!.Entries.Single().Dead)
            .Should().Be((true, false, 1, true));
        view.Combat.Entries.Single().Conditions.Should().Equal("Aturdido");
    }

    [Fact]
    public void Down_and_current_are_marked()
    {
        var view = Project(State([Tok("a", hpCurrent: 0), Tok("b")], Combat("b", "a", "b")));
        view.Tokens.Select(t => (t.Down, t.Current)).Should().Equal((true, false), (false, true));
    }

    [Fact]
    public void Strip_keeps_combat_order()
    {
        var view = Project(State([Tok("a"), Tok("b"), Tok("c")], Combat("a", "c", "a", "b")));
        view.Combat!.Entries.Select(e => e.TokenId).Should().Equal("c", "a", "b");
    }

    [Fact]
    public void No_combat_gives_null_combat() => Project(State([Tok("a")])).Combat.Should().BeNull();

    [Fact]
    public void Look_portrait_is_copied_only_for_visible_tokens()
    {
        var view = Project(State([Tok("a"), Tok("h", hidden: true)]));
        view.Tokens.Select(t => t.PortraitPath).Should().Equal("retratos/a.png");
    }

    [Fact]
    public void Map_data_is_copied()
    {
        var view = Project(State([]));
        (view.MapPath, view.ImagePath, view.Width, view.Height, view.Frame).Should().Be(("mapas/cueva.md", "mapas/cueva.png", 2000.0, 1000.0, Frame));
    }

    [Fact]
    public void Expired_conditions_are_hidden_from_players()
    {
        var active = new TokenCondition("c1", "stunned", "Aturdido", null, null, false);
        var expired = new TokenCondition("c2", "prone", "Derribado", null, null, true);
        var view = Project(State([Tok("e", conditions: [active, expired])], Combat("e", "e")));
        view.Tokens.Single().Conditions.Should().Be(1);
        view.Combat!.Entries.Single().Conditions.Should().Equal("Aturdido");
    }

    [Fact]
    public void Initial_comes_from_the_player_name()
    {
        var view = Project(State([Tok("a", alias: "Figura")]));
        view.Tokens.Single().Initial.Should().Be("F");
    }

    [Fact]
    public void Initial_is_question_mark_for_empty_name()
    {
        var t = Tok("a") with { Name = "" };
        Project(State([t])).Tokens.Single().Initial.Should().Be("?");
    }

    [Fact]
    public void Aliased_token_never_sends_its_portrait()
    {
        var view = Project(State([Tok("a", alias: "Sombra"), Tok("b")]));
        view.Tokens.Select(t => t.PortraitPath).Should().Equal(null, "retratos/b.png");
    }

    [Fact]
    public void Pc_hp_without_max_shows_temp() => Hp(Tok("p", TokenSide.Pc, 12, 3), null).Should().Be("12 (+3)");
}
