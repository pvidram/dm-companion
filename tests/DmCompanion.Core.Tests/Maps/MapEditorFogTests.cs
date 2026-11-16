using DmCompanion.Core.Maps;

namespace DmCompanion.Core.Tests.Maps;

public class MapEditorFogTests
{
    private static readonly double[] Square = [0, 0, 10, 0, 10, 10, 0, 10];

    private static MapEditor NewEditor()
    {
        var n = 0;
        return new MapEditor(MapState.Empty, 1000, 500, () => $"id{++n}");
    }

    [Fact]
    public void AddFogZone_adds_a_covered_undoable_zone()
    {
        var ed = NewEditor();

        var zone = ed.AddFogZone(Square);

        zone!.IsRevealed.Should().BeFalse();
        ed.State.FogZones.Should().ContainSingle().Which.Id.Should().Be(zone.Id);
        ed.CanUndo.Should().BeTrue();
    }

    [Fact]
    public void AddFogZone_keeps_its_own_copy_of_the_points()
    {
        var ed = NewEditor();
        var points = new List<double>(Square);

        ed.AddFogZone(points);
        points[0] = 99;

        ed.State.FogZones.Single().Points.Should().Equal(Square);
    }

    [Fact]
    public void AddFogZone_with_an_invalid_polygon_returns_null_and_changes_nothing()
    {
        var ed = NewEditor();
        var before = ed.State;

        ed.AddFogZone([0, 0, 10, 0]).Should().BeNull();

        ed.State.Should().BeSameAs(before);
        ed.CanUndo.Should().BeFalse();
    }

    [Fact]
    public void SetFogRevealed_is_undoable_and_redoable()
    {
        var ed = NewEditor();
        var zone = ed.AddFogZone(Square)!;

        ed.SetFogRevealed(zone.Id, true).Should().BeTrue();
        ed.State.FogZones[0].IsRevealed.Should().BeTrue();
        ed.Undo();
        ed.State.FogZones[0].IsRevealed.Should().BeFalse();
        ed.Redo();
        ed.State.FogZones[0].IsRevealed.Should().BeTrue();
    }

    [Fact]
    public void SetFogRevealed_with_the_same_value_returns_false()
    {
        var ed = NewEditor();
        var zone = ed.AddFogZone(Square)!;

        ed.SetFogRevealed(zone.Id, false).Should().BeFalse();
    }

    [Fact]
    public void SetFogRevealed_with_unknown_id_returns_false()
    {
        NewEditor().SetFogRevealed("nope", true).Should().BeFalse();
    }

    [Fact]
    public void Undoing_an_erased_zone_restores_it_at_its_index()
    {
        var ed = NewEditor();
        ed.AddFogZone(Square);
        var second = ed.AddFogZone(Square)!;
        ed.AddFogZone(Square);

        ed.EraseFogZone(second.Id).Should().BeTrue();
        ed.State.FogZones.Should().HaveCount(2);
        ed.Undo();

        ed.State.FogZones[1].Id.Should().Be(second.Id);
    }

    [Fact]
    public void EraseFogZone_with_unknown_id_returns_false()
    {
        NewEditor().EraseFogZone("nope").Should().BeFalse();
    }

    [Fact]
    public void Undoing_the_only_zone_leaves_fog_null()
    {
        var ed = NewEditor();
        ed.AddFogZone(Square);

        ed.Undo();

        ed.State.Fog.Should().BeNull();
    }

    [Fact]
    public void Erasing_the_only_zone_leaves_fog_null()
    {
        var ed = NewEditor();
        var zone = ed.AddFogZone(Square)!;

        ed.EraseFogZone(zone.Id);

        ed.State.Fog.Should().BeNull();
    }

    [Fact]
    public void Undo_after_a_stroke_and_a_zone_removes_only_the_zone()
    {
        var ed = NewEditor();
        ed.AddStroke("#000", 3, [0, 0, 1, 10, 10, 1]);
        ed.AddFogZone(Square);

        ed.Undo();

        ed.State.FogZones.Should().BeEmpty();
        ed.State.Strokes.Should().HaveCount(1);
    }
}
