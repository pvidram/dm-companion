using DmCompanion.Core.Combat;
using DmCompanion.Core.Maps;

namespace DmCompanion.Core.Tests;

public class MapEditorTests
{
    private static readonly double[] Pts = [0, 0, 1, 10, 10, 1];

    private static MapEditor NewEditor(double w = 1000, double h = 500)
    {
        var n = 0;
        return new MapEditor(MapState.Empty, w, h, () => $"id{++n}");
    }

    private static TokenDraft Generic(int size = 1) => new("Goblin", null, TokenSide.Enemy, size, 15, 7);

    [Fact]
    public void AddToken_keeps_the_draft_alias_trimmed()
    {
        var ed = NewEditor();
        var t = ed.AddToken(Generic() with { Alias = "  Sombra " }, 10, 10);
        t.Alias.Should().Be("Sombra");
    }

    [Fact]
    public void AddToken_with_blank_alias_stores_null()
    {
        var ed = NewEditor();
        var t = ed.AddToken(Generic() with { Alias = "   " }, 10, 10);
        t.Alias.Should().BeNull();
    }

    [Fact]
    public void MoveToken_clamps_to_the_bounds()
    {
        var ed = NewEditor();
        var t = ed.AddToken(Generic(), 10, 10);
        ed.MoveToken(t.Id, -50, 900).Should().BeTrue();
        ed.State.Tokens[0].X.Should().Be(0);
        ed.State.Tokens[0].Y.Should().Be(500);
        ed.MoveToken(t.Id, 5000, 20);
        ed.State.Tokens[0].X.Should().Be(1000);
    }

    [Fact]
    public void Unknown_dimensions_do_not_clamp()
    {
        var ed = NewEditor(0, 0);
        var t = ed.AddToken(Generic(), -5, 9000);
        t.X.Should().Be(-5);
        t.Y.Should().Be(9000);
        ed.MoveToken(t.Id, 1234, -1);
        ed.State.Tokens[0].X.Should().Be(1234);
        ed.State.Tokens[0].Y.Should().Be(-1);
    }

    [Fact]
    public void MoveToken_with_unknown_id_returns_false_and_changes_nothing()
    {
        var ed = NewEditor();
        ed.AddToken(Generic(), 10, 10);
        var before = ed.State;
        ed.MoveToken("nope", 1, 1).Should().BeFalse();
        ed.State.Should().BeSameAs(before);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public void AddToken_throws_for_invalid_size(int size) =>
        FluentActions.Invoking(() => NewEditor().AddToken(Generic(size), 0, 0))
            .Should().Throw<ArgumentOutOfRangeException>();

    [Fact]
    public void AddToken_throws_for_blank_name() =>
        FluentActions.Invoking(() => NewEditor().AddToken(new TokenDraft(" ", null, TokenSide.Pc, 1, null, null), 0, 0))
            .Should().Throw<ArgumentException>();

    [Fact]
    public void UpdateToken_on_a_source_token_keeps_name_ac_and_hp()
    {
        var ed = NewEditor();
        var t = ed.AddToken(new TokenDraft("Goblin", "Bestiario/Goblin.md", TokenSide.Enemy, 1, 15, 7), 0, 0);
        ed.UpdateToken(t.Id, new TokenEdit(TokenSide.Ally, 2, "Otro", 1, 1)).Should().BeTrue();
        var u = ed.State.Tokens[0];
        u.Side.Should().Be(TokenSide.Ally);
        u.Size.Should().Be(2);
        u.Name.Should().Be("Goblin");
        u.Ac.Should().Be(15);
        u.Hp.Should().Be(7);
    }

    [Fact]
    public void UpdateToken_on_a_generic_token_changes_all_fields()
    {
        var ed = NewEditor();
        var t = ed.AddToken(Generic(), 0, 0);
        ed.UpdateToken(t.Id, new TokenEdit(TokenSide.Pc, 3, "Hero", 18, 40));
        var u = ed.State.Tokens[0];
        (u.Side, u.Size, u.Name, u.Ac, u.Hp).Should().Be((TokenSide.Pc, 3, "Hero", 18, 40));
    }

    [Fact]
    public void UpdateToken_with_unknown_id_returns_false() =>
        NewEditor().UpdateToken("x", new TokenEdit(TokenSide.Pc, 1, "a", null, null)).Should().BeFalse();

    [Fact]
    public void RemoveToken_removes_only_existing_tokens()
    {
        var ed = NewEditor();
        var t = ed.AddToken(Generic(), 0, 0);
        ed.RemoveToken("x").Should().BeFalse();
        ed.RemoveToken(t.Id).Should().BeTrue();
        ed.State.Tokens.Should().BeEmpty();
    }

    [Fact]
    public void AddStroke_with_a_point_list_that_is_not_a_multiple_of_three_returns_null()
    {
        var ed = NewEditor();
        ed.AddStroke("#fff", 3, [1, 2, 1, 3, 4, 1, 5]).Should().BeNull();
        ed.State.Strokes.Should().BeEmpty();
        ed.CanUndo.Should().BeFalse();
    }

    [Fact]
    public void AddStroke_with_fewer_than_two_points_returns_null()
    {
        var ed = NewEditor();
        ed.AddStroke("#fff", 3, [1, 2, 1, 3, 4]).Should().BeNull();
        ed.State.Strokes.Should().BeEmpty();
        ed.CanUndo.Should().BeFalse();
    }

    [Fact]
    public void Undo_and_redo_an_added_stroke()
    {
        var ed = NewEditor();
        ed.AddStroke("#fff", 3, Pts);
        ed.Undo().Should().BeTrue();
        ed.State.Strokes.Should().BeEmpty();
        ed.CanRedo.Should().BeTrue();
        ed.Redo().Should().BeTrue();
        ed.State.Strokes.Should().HaveCount(1);
    }

    [Fact]
    public void Undo_restores_an_erased_stroke()
    {
        var ed = NewEditor();
        var s = ed.AddStroke("#fff", 3, Pts)!;
        ed.EraseStroke(s.Id).Should().BeTrue();
        ed.State.Strokes.Should().BeEmpty();
        ed.Undo();
        ed.State.Strokes.Should().ContainSingle().Which.Id.Should().Be(s.Id);
        ed.EraseStroke("nope").Should().BeFalse();
    }

    [Fact]
    public void A_new_stroke_after_undo_clears_redo()
    {
        var ed = NewEditor();
        ed.AddStroke("#fff", 3, Pts);
        ed.Undo();
        ed.AddStroke("#000", 3, Pts);
        ed.CanRedo.Should().BeFalse();
        ed.Redo().Should().BeFalse();
    }

    [Fact]
    public void MoveToken_is_not_undoable()
    {
        var ed = NewEditor();
        var t = ed.AddToken(Generic(), 0, 0);
        ed.MoveToken(t.Id, 5, 5);
        ed.CanUndo.Should().BeFalse();
        ed.Undo().Should().BeFalse();
    }

    [Fact]
    public void SetGrid_replaces_the_grid()
    {
        var ed = NewEditor();
        var g = new GridSettings(true, 50, 1, 2);
        ed.SetGrid(g);
        ed.State.Grid.Should().Be(g);
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(-5, 0, 0)]
    [InlineData(double.NaN, 0, 0)]
    [InlineData(double.PositiveInfinity, 0, 0)]
    [InlineData(50, double.NaN, 0)]
    [InlineData(50, 0, double.PositiveInfinity)]
    public void SetGrid_rejects_invalid_values(double cell, double ox, double oy)
    {
        var ed = NewEditor();
        var act = () => ed.SetGrid(new GridSettings(true, cell, ox, oy));
        act.Should().Throw<ArgumentOutOfRangeException>();
        ed.State.Grid.Should().Be(GridSettings.Default);
    }

    [Fact]
    public void Undo_restores_an_erased_stroke_at_its_original_index()
    {
        var ed = NewEditor();
        var a = ed.AddStroke("#fff", 3, Pts)!;
        var b = ed.AddStroke("#000", 3, Pts)!;
        ed.EraseStroke(a.Id);
        ed.Undo();
        ed.State.Strokes.Select(s => s.Id).Should().Equal(a.Id, b.Id);
        ed.Redo();
        ed.State.Strokes.Select(s => s.Id).Should().Equal(b.Id);
    }

    [Fact]
    public void UpdateToken_on_a_generic_token_throws_for_blank_name()
    {
        var ed = NewEditor();
        var t = ed.AddToken(Generic(), 0, 0);
        FluentActions.Invoking(() => ed.UpdateToken(t.Id, new TokenEdit(TokenSide.Pc, 1, " ", null, null)))
            .Should().Throw<ArgumentException>();
        ed.State.Tokens[0].Name.Should().Be("Goblin");
    }

    [Theory]
    [InlineData(5, 5)]
    [InlineData(99, 11)]
    [InlineData(-3, 0)]
    public void SetHp_clamps_and_stores(int value, int expected)
    {
        var ed = NewEditor();
        var t = ed.AddToken(Generic(), 10, 10);

        ed.SetHp(t.Id, value, max: 11);

        ed.State.Tokens[0].HpCurrent.Should().Be(expected);
    }

    [Fact]
    public void SetHp_stores_the_value_even_when_it_equals_the_maximum()
    {
        var ed = NewEditor();
        var t = ed.AddToken(Generic(), 10, 10);

        ed.SetHp(t.Id, 11, max: 11);

        ed.State.Tokens[0].HpCurrent.Should().Be(11);
    }

    [Fact]
    public void SetHp_clears_death_saves_when_rising_from_zero()
    {
        var ed = NewEditor();
        var t = ed.AddToken(Generic(), 10, 10);
        ed.ReplaceState(s => s with { Tokens = [s.Tokens[0] with { HpCurrent = 0, DeathSaves = new DeathSaves(1, 2) }] });

        ed.SetHp(t.Id, 3, max: 11);

        ed.State.Tokens[0].DeathSaves.Should().BeNull();
    }

    [Fact]
    public void SetHp_keeps_death_saves_while_staying_at_zero()
    {
        var ed = NewEditor();
        var t = ed.AddToken(Generic(), 10, 10);
        ed.ReplaceState(s => s with { Tokens = [s.Tokens[0] with { HpCurrent = 0, DeathSaves = new DeathSaves(1, 2) }] });

        ed.SetHp(t.Id, 0, max: 11);

        ed.State.Tokens[0].DeathSaves.Should().Be(new DeathSaves(1, 2));
    }

    [Fact]
    public void SetHp_returns_false_for_an_unknown_token() =>
        NewEditor().SetHp("nope", 5, 11).Should().BeFalse();

    [Fact]
    public void SetHp_is_not_undoable()
    {
        var ed = NewEditor();
        var t = ed.AddToken(Generic(), 10, 10);

        ed.SetHp(t.Id, 5, 11);

        ed.CanUndo.Should().BeFalse();
    }

    [Fact]
    public void RemoveToken_in_combat_removes_its_entry_and_passes_the_turn()
    {
        var ed = NewEditor();
        var a = ed.AddToken(Generic(), 1, 1);
        var b = ed.AddToken(Generic(), 2, 2);
        ed.ReplaceState(s => TurnOps.Start(s, [new(a.Id, 18), new(b.Id, 9)], () => "entry" + Guid.NewGuid()).State);
        ed.RemoveToken(a.Id).Should().BeTrue();
        ed.State.Combat!.Entries.Select(e => e.TokenId).Should().Equal(b.Id);
        ed.State.Combat.CurrentEntryId.Should().Be(ed.State.Combat.Entries[0].Id);
    }

    [Fact]
    public void ReplaceState_returns_false_when_nothing_changes()
    {
        var ed = NewEditor();
        var same = ed.ReplaceState(s => s);
        var copy = ed.ReplaceState(s => s with { Version = s.Version });
        (same, copy).Should().Be((false, true));
    }
}
