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

    [Fact] public void AddFogZone_defaults_to_a_total_zone() =>
        NewEditor().AddFogZone(Square)!.Type.Should().BeNull();

    [Fact] public void AddFogZone_can_add_an_npc_zone() =>
        NewEditor().AddFogZone(Square, FogType.Npc)!.EffectiveType.Should().Be(FogType.Npc);

    [Fact] public void SetFogType_changes_the_type_of_the_zone()
    {
        var ed = NewEditor();
        var id = ed.AddFogZone(Square)!.Id;
        ed.SetFogType(id, FogType.Npc).Should().BeTrue();
        ed.State.FogZones.Single().EffectiveType.Should().Be(FogType.Npc);
    }

    [Fact] public void SetFogType_with_the_same_type_returns_false_and_adds_no_undo_step()
    {
        var ed = NewEditor();
        var id = ed.AddFogZone(Square)!.Id;
        ed.Undo(); ed.Redo(); // deja una sola operación en la pila
        var before = ed.State;
        ed.SetFogType(id, FogType.Total).Should().BeFalse();
        ed.State.Should().BeSameAs(before);
    }

    [Fact] public void SetFogType_with_an_unknown_id_returns_false() =>
        NewEditor().SetFogType("nope", FogType.Npc).Should().BeFalse();

    [Fact] public void Undo_of_SetFogType_restores_total_and_redo_reapplies_npc()
    {
        var ed = NewEditor();
        var id = ed.AddFogZone(Square)!.Id;
        ed.SetFogType(id, FogType.Npc);
        ed.Undo();
        var afterUndo = ed.State.FogZones.Single().EffectiveType;
        ed.Redo();
        (afterUndo, ed.State.FogZones.Single().EffectiveType).Should().Be((FogType.Total, FogType.Npc));
    }

    [Fact] public void SetFogType_keeps_the_revealed_flag()
    {
        var ed = NewEditor();
        var id = ed.AddFogZone(Square)!.Id;
        ed.SetFogRevealed(id, true);
        ed.SetFogType(id, FogType.Npc);
        ed.State.FogZones.Single().IsRevealed.Should().BeTrue();
    }

    [Fact] public void Undoing_a_reveal_and_then_a_type_change_restores_both()
    {
        var ed = NewEditor();
        var id = ed.AddFogZone(Square)!.Id;
        ed.SetFogType(id, FogType.Npc);
        ed.SetFogRevealed(id, true);
        ed.Undo(); ed.Undo();
        var zone = ed.State.FogZones.Single();
        (zone.IsRevealed, zone.EffectiveType).Should().Be((false, FogType.Total));
    }

    [Fact] public void Erasing_and_undoing_an_npc_zone_keeps_its_type()
    {
        var ed = NewEditor();
        var id = ed.AddFogZone(Square, FogType.Npc)!.Id;
        ed.EraseFogZone(id);
        ed.Undo();
        ed.State.FogZones.Single().EffectiveType.Should().Be(FogType.Npc);
    }
}
