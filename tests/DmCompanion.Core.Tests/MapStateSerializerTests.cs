using DmCompanion.Core.Combat;
using DmCompanion.Core.Maps;

namespace DmCompanion.Core.Tests;

public class MapStateSerializerTests
{
    private const string Example = """
        {
          "version": 1,
          "grid": { "enabled": false, "cellSize": 70, "offsetX": 0, "offsetY": 0 },
          "tokens": [
            { "id": "t-8f3a", "name": "Goblin 2", "source": "Personajes/Goblin.md",
              "side": "enemy", "size": 1, "x": 455, "y": 280 },
            { "id": "t-91c0", "name": "Bandido", "source": null,
              "side": "enemy", "size": 1, "x": 525, "y": 280, "ac": 12, "hp": 11 }
          ],
          "strokes": [
            { "id": "s-02d1", "color": "#2b2b2b", "width": 6,
              "points": [10.5, 20.0, 0.5, 12.0, 24.5, 0.6] }
          ]
        }
        """;

    [Fact]
    public void Read_parses_spec_example()
    {
        var result = MapStateSerializer.Read(Example);

        result.Status.Should().Be(MapStateReadStatus.Ok);
        result.State.Tokens.Should().HaveCount(2);
        result.State.Strokes.Should().ContainSingle().Which.Points.Should().HaveCount(6);
        result.State.Tokens[0].Should().Match<MapToken>(t => t.Source == "Personajes/Goblin.md" && t.Side == TokenSide.Enemy && t.Ac == null);
        result.State.Tokens[1].Should().Match<MapToken>(t => t.Source == null && t.Ac == 12 && t.Hp == 11);
    }

    [Theory]
    [InlineData("{")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("null")]
    [InlineData("{\"version\": 1, \"tokens\": [{\"side\": \"dragon\"}]}")]
    [InlineData("{\"version\": 1, \"tokens\": [null]}")]
    [InlineData("{\"version\": 1, \"strokes\": [null]}")]
    [InlineData("{\"version\": 1, \"tokens\": [{\"name\": \"A\", \"side\": \"pc\", \"size\": 1}]}")]
    [InlineData("{\"version\": 1, \"tokens\": [{\"id\": \"t\", \"side\": \"pc\", \"size\": 1}]}")]
    [InlineData("{\"version\": 1, \"tokens\": [{\"id\": \"t\", \"name\": \"A\", \"side\": 7, \"size\": 1}]}")]
    [InlineData("{\"version\": 1, \"tokens\": [{\"id\": \"t\", \"name\": \"A\", \"side\": \"pc\", \"size\": 0}]}")]
    [InlineData("{\"version\": 1, \"tokens\": [{\"id\": \"t\", \"name\": \"A\", \"side\": \"pc\", \"size\": 4}]}")]
    [InlineData("{\"version\": 1, \"strokes\": [{\"color\": \"#000\", \"width\": 1, \"points\": []}]}")]
    [InlineData("{\"version\": 1, \"strokes\": [{\"id\": \"s\", \"color\": \"#000\", \"width\": 1, \"points\": null}]}")]
    [InlineData("{\"version\": 1, \"grid\": {\"enabled\": true, \"cellSize\": 0}}")]
    [InlineData("{\"version\": 1, \"grid\": {\"enabled\": true, \"cellSize\": -5}}")]
    [InlineData("{\"version\": 1, \"grid\": {\"enabled\": true, \"cellSize\": \"NaN\"}}")]
    [InlineData("{\"version\": 1, \"grid\": {\"enabled\": true, \"cellSize\": 50, \"offsetX\": \"Infinity\"}}")]
    [InlineData("{\"version\": 1, \"grid\": {\"enabled\": true, \"cellSize\": 1e999}}")]
    public void Read_returns_invalid_with_empty_state_for_bad_json(string json)
    {
        var result = MapStateSerializer.Read(json);

        result.Status.Should().Be(MapStateReadStatus.Invalid);
        result.State.Should().BeSameAs(MapState.Empty);
    }

    [Fact]
    public void Read_flags_future_version_and_keeps_content()
    {
        var result = MapStateSerializer.Read(Example.Replace("\"version\": 1", "\"version\": 4"));

        result.Status.Should().Be(MapStateReadStatus.FutureVersion);
        result.State.Version.Should().Be(4);
        result.State.Tokens.Should().HaveCount(2);
    }

    private const string FutureShape =
        "{\"version\": 4, \"tokens\": [{\"id\": \"a\", \"name\": \"A\", \"size\": 1, \"x\": 0, \"y\": 0, "
        + "\"conditions\": [null, {\"id\": null, \"label\": \"X\"}, {\"id\": \"c\", \"label\": \"Y\"}]}], \"combat\": {\"round\": 1}}";

    [Fact]
    public void Read_future_version_keeps_the_status_with_an_odd_shape()
    {
        MapStateSerializer.Read(FutureShape).Status.Should().Be(MapStateReadStatus.FutureVersion);
    }

    [Fact]
    public void Read_future_version_turns_null_entries_into_an_empty_list()
    {
        MapStateSerializer.Read(FutureShape).State.Combat!.Entries.Should().BeEmpty();
    }

    [Fact]
    public void Read_future_version_drops_null_and_incomplete_conditions()
    {
        MapStateSerializer.Read(FutureShape).State.Tokens[0].ConditionList.Select(c => c.Id).Should().Equal("c");
    }

    [Fact]
    public void Read_future_version_does_not_count_repairs()
    {
        MapStateSerializer.Read(FutureShape).Repairs.Should().Be(0);
    }

    [Fact]
    public void Read_fills_missing_sections_with_empty_values()
    {
        var result = MapStateSerializer.Read("{\"version\": 1}");

        result.Status.Should().Be(MapStateReadStatus.Ok);
        result.State.Should().BeEquivalentTo(MapState.Empty);
    }

    [Fact]
    public void Write_then_Read_round_trips()
    {
        var state = new MapState(
            MapState.CurrentVersion,
            new GridSettings(true, 50, 5, -3),
            [
                new MapToken("t-1", "Lyra", "Personajes/Lyra.md", TokenSide.Pc, 1, 100.5, 200, null, null),
                new MapToken("t-2", "Bandido", null, TokenSide.Ally, 2, 10, 20, 12, 11),
            ],
            [new MapStroke("s-1", "#ff0000", 4, [1, 2, 0.5, 3, 4, 0.7])]);

        MapStateSerializer.Read(MapStateSerializer.Write(state)).State.Should().BeEquivalentTo(state);
    }

    [Fact]
    public void Write_uses_camel_case_and_lowercase_sides()
    {
        var state = MapState.Empty with
        {
            Tokens = [new MapToken("t-1", "A", null, TokenSide.Enemy, 1, 0, 0, null, null)],
        };

        var json = MapStateSerializer.Write(state);

        json.Should().Contain("\"cellSize\":70").And.Contain("\"side\":\"enemy\"").And.NotContain("\n");
    }

    [Fact]
    public void Write_omits_null_ac_and_hp()
    {
        var state = MapState.Empty with
        {
            Tokens = [new MapToken("t-1", "Lyra", "Personajes/Lyra.md", TokenSide.Pc, 1, 0, 0, null, null)],
        };

        MapStateSerializer.Write(state).Should().NotContain("\"ac\"").And.NotContain("\"hp\"");
    }

    [Fact]
    public void Write_rounds_positions_to_one_decimal_and_pressure_to_two()
    {
        var state = MapState.Empty with
        {
            Tokens = [new MapToken("t-1", "A", null, TokenSide.Pc, 1, 10.349, 20.351, null, null)],
            Strokes = [new MapStroke("s-1", "#000", 4, [1.2345, 2.3456, 0.56789, 3.04, 4.06, 0.944])],
        };

        var written = MapStateSerializer.Read(MapStateSerializer.Write(state)).State;

        (written.Tokens[0].X, written.Tokens[0].Y).Should().Be((10.3, 20.4));
        written.Strokes[0].Points.Should().Equal(1.2, 2.3, 0.57, 3.0, 4.1, 0.94);
    }

    [Theory]
    [InlineData("""{"version":1,"strokes":[{"id":"s","color":"#000","width":3,"points":[1,2,0.5,3]}]}""")]
    [InlineData("""{"version":1,"strokes":[{"id":"s","color":null,"width":3,"points":[1,2,0.5,3,4,0.5]}]}""")]
    public void Read_rejects_strokes_with_malformed_points_or_without_color(string json) =>
        MapStateSerializer.Read(json).Status.Should().Be(MapStateReadStatus.Invalid);

    private static string StateWithToken(string extra) =>
        """{"version":1,"tokens":[{"id":"t","name":"A","side":"pc","size":1,"x":0,"y":0""" + extra + "}]}";

    [Fact]
    public void Read_without_hpCurrent_leaves_it_null() =>
        MapStateSerializer.Read(StateWithToken("")).State.Tokens[0].HpCurrent.Should().BeNull();

    [Fact]
    public void Read_accepts_hpCurrent_zero_and_positive()
    {
        var read = MapStateSerializer.Read(StateWithToken(""","hpCurrent":0"""));

        (read.Status, read.State.Tokens[0].HpCurrent).Should().Be((MapStateReadStatus.Ok, 0));
    }

    [Theory]
    [InlineData(""","hpCurrent":-1""")]
    [InlineData(""","hpCurrent":1.5""")]
    [InlineData(""","hpCurrent":"7" """)]
    public void Read_rejects_invalid_hpCurrent(string extra) =>
        MapStateSerializer.Read(StateWithToken(extra)).Status.Should().Be(MapStateReadStatus.Invalid);

    [Fact]
    public void Hp_current_round_trips()
    {
        var state = MapState.Empty with
        {
            Tokens = [new MapToken("t-1", "A", null, TokenSide.Pc, 1, 0, 0, null, 11, 7)],
        };

        MapStateSerializer.Read(MapStateSerializer.Write(state)).State.Tokens[0].HpCurrent.Should().Be(7);
    }

    [Fact]
    public void Write_omits_null_hpCurrent()
    {
        var state = MapState.Empty with
        {
            Tokens = [new MapToken("t-1", "A", null, TokenSide.Pc, 1, 0, 0, null, null)],
        };

        MapStateSerializer.Write(state).Should().NotContain("hpCurrent");
    }

    [Theory]
    [InlineData(""","ac":-1""")]
    [InlineData(""","hp":-5""")]
    public void Read_rejects_negative_ac_and_hp(string extra) =>
        MapStateSerializer.Read(StateWithToken(extra)).Status.Should().Be(MapStateReadStatus.Invalid);

    [Fact]
    public void Read_accepts_ac_and_hp_zero_and_positive()
    {
        var read = MapStateSerializer.Read(StateWithToken(""","ac":0,"hp":12"""));

        (read.Status, read.State.Tokens[0].Ac, read.State.Tokens[0].Hp).Should().Be((MapStateReadStatus.Ok, 0, 12));
    }

    [Fact]
    public void Version_1_file_reads_as_version_2_with_empty_combat_fields()
    {
        var result = MapStateSerializer.Read(Example.Replace("\"hp\": 11", "\"hp\": 11, \"hpCurrent\": 4"));

        var expected = new MapState(
            MapState.CurrentVersion,
            new GridSettings(false, 70, 0, 0),
            [
                new MapToken("t-8f3a", "Goblin 2", "Personajes/Goblin.md", TokenSide.Enemy, 1, 455, 280, null, null),
                new MapToken("t-91c0", "Bandido", null, TokenSide.Enemy, 1, 525, 280, 12, 11, 4),
            ],
            [new MapStroke("s-02d1", "#2b2b2b", 6, [10.5, 20.0, 0.5, 12.0, 24.5, 0.6])]);
        (result.Status, result.Repairs).Should().Be((MapStateReadStatus.Ok, 0));
        result.State.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void Read_applies_repair_and_reports_the_count()
    {
        const string json = """
            {"version":2,"tokens":[{"id":"a","name":"A","side":"pc","size":1,"x":0,"y":0}],
             "combat":{"round":1,"currentEntryId":"e1","entries":[{"id":"e1","tokenId":"a","initiative":9},{"id":"e2","tokenId":"ghost","initiative":3}]}}
            """;

        var result = MapStateSerializer.Read(json);

        (result.Status, result.Repairs, result.State.Combat!.Entries.Count).Should().Be((MapStateReadStatus.Ok, 1, 1));
    }

    [Fact]
    public void Read_with_null_condition_repairs_instead_of_throwing()
    {
        const string json = """{"version":2,"tokens":[{"id":"a","name":"A","side":"pc","size":1,"x":0,"y":0,"conditions":[null]}]}""";

        var result = MapStateSerializer.Read(json);

        (result.Status, result.Repairs > 0, result.State.Tokens[0].ConditionList.Count).Should().Be((MapStateReadStatus.Ok, true, 0));
    }

    [Fact]
    public void Version_2_state_round_trips()
    {
        var condition = new TokenCondition("c1", "poisoned", "Envenenado", 1, new ConditionDuration("e2", 1), false);
        var token = new MapToken("t1", "Goblin", null, TokenSide.Enemy, 1, 10, 20, null, null, null, 5, [condition], new DeathSaves(1, 2));
        var other = new MapToken("t2", "Elfa", null, TokenSide.Pc, 1, 30, 40, null, null);
        var combat = new CombatState(2, "e2", [new CombatEntry("e1", "t2", 18, CombatPhase.Fast), new CombatEntry("e2", "t1", 15), new CombatEntry("e3", "t2", null)]);
        var s = new MapState(MapState.CurrentVersion, GridSettings.Default, [token, other], [], combat);

        var read = MapStateSerializer.Read(MapStateSerializer.Write(s)).State;

        read.Should().BeEquivalentTo(s);
    }

    [Fact]
    public void Written_json_omits_empty_combat_fields()
    {
        var json = MapStateSerializer.Write(MapState.Empty with { Tokens = [new MapToken("t", "A", null, TokenSide.Pc, 1, 0, 0, null, null)] });

        json.Should().Contain("\"version\":2").And.NotContainAny("combat", "hpTemp", "conditions", "deathSaves");
    }

    [Fact]
    public void Version_4_is_future_version()
    {
        MapStateSerializer.Read("{\"version\": 4}").Status.Should().Be(MapStateReadStatus.FutureVersion);
    }

    private static CombatSnapshot Snapshot(CombatState? combat, params TokenCombatFields[] tokens) => new(combat, tokens);

    [Fact]
    public void Dead_round_trips()
    {
        var s = MapState.Empty with { Tokens = [new MapToken("t", "A", null, TokenSide.Enemy, 1, 0, 0, null, null, Dead: true)] };

        MapStateSerializer.Read(MapStateSerializer.Write(s)).State.Tokens[0].IsDead.Should().BeTrue();
    }

    [Fact]
    public void Dead_false_is_omitted()
    {
        var s = MapState.Empty with { Tokens = [new MapToken("t", "A", null, TokenSide.Enemy, 1, 0, 0, null, null)] };

        MapStateSerializer.Write(s).Should().NotContain("dead");
    }

    [Fact]
    public void Log_round_trips()
    {
        var condition = new TokenCondition("c1", "poisoned", "Envenenado", 1, new ConditionDuration("e1", 1), false);
        var before = Snapshot(new CombatState(1, "e1", [new CombatEntry("e1", "t1", 15)]), new TokenCombatFields("t1", 7, 2, [condition], new DeathSaves(1, 0), null));
        var after = Snapshot(new CombatState(2, "e1", [new CombatEntry("e1", "t1", 15)]), new TokenCombatFields("t1", 0, null, null, null, true));
        var log = new[] { new CombatLogEntry("l1", 1, "Daño", before, after, true), new CombatLogEntry("l2", 1, "Empieza", null, after, false, false) };
        var token = new MapToken("t1", "Goblin", null, TokenSide.Enemy, 1, 0, 0, null, null);
        var s = new MapState(MapState.CurrentVersion, GridSettings.Default, [token], [], new CombatState(2, "e1", [new CombatEntry("e1", "t1", 15)], log));

        MapStateSerializer.Read(MapStateSerializer.Write(s)).State.Should().BeEquivalentTo(s);
    }

    [Fact]
    public void LastCombat_round_trips()
    {
        var token = new MapToken("t1", "Goblin", null, TokenSide.Enemy, 1, 0, 0, null, null);
        var combat = new CombatState(3, "e1", [new CombatEntry("e1", "t1", 15)], [new CombatLogEntry("l1", 3, "Daño", null, null, false, false)]);
        var s = new MapState(MapState.CurrentVersion, GridSettings.Default, [token], [], null, Snapshot(combat, new TokenCombatFields("t1", 4, null, null, null, null)));

        MapStateSerializer.Read(MapStateSerializer.Write(s)).State.Should().BeEquivalentTo(s);
    }

    [Fact]
    public void Hidden_and_alias_round_trip()
    {
        var token = new MapToken("t1", "Goblin", null, TokenSide.Enemy, 1, 10, 20, null, null, Hidden: true, Alias: "Sombra");
        var json = MapStateSerializer.Write(MapState.Empty with { Tokens = [token] });

        var read = MapStateSerializer.Read(json).State.Tokens.Should().ContainSingle().Which;

        read.Hidden.Should().BeTrue();
        read.Alias.Should().Be("Sombra");
    }

    [Fact]
    public void Visible_token_without_alias_omits_both_fields()
    {
        var token = new MapToken("t1", "Goblin", null, TokenSide.Enemy, 1, 10, 20, null, null);
        var json = MapStateSerializer.Write(MapState.Empty with { Tokens = [token] });

        json.Should().NotContain("\"hidden\"").And.NotContain("\"alias\"");
    }

    private static readonly double[] Square = [0, 0, 10, 0, 10, 10, 0, 10];

    [Fact]
    public void Written_json_without_fog_zones_has_no_fog_and_version_2()
    {
        var json = MapStateSerializer.Write(MapState.Empty);

        json.Should().Contain("\"version\":2").And.NotContain("\"fog\"");
    }

    [Fact]
    public void Written_json_with_a_covered_zone_has_fog_and_version_3_without_revealed()
    {
        var json = MapStateSerializer.Write(MapState.Empty with { Fog = [new MapFogZone("z1", Square)] });

        json.Should().Contain("\"fog\"").And.Contain("\"version\":3").And.NotContain("\"revealed\"");
    }

    [Fact]
    public void Written_json_with_a_revealed_zone_has_revealed_true()
    {
        var json = MapStateSerializer.Write(MapState.Empty with { Fog = [new MapFogZone("z1", Square, true)] });

        json.Should().Contain("\"revealed\":true");
    }

    [Fact]
    public void Fog_zones_round_trip_with_points_rounded_to_one_decimal()
    {
        var state = MapState.Empty with { Fog = [new MapFogZone("a", [0.04, 0.06, 10, 0, 10, 10.26]), new MapFogZone("b", Square, true)] };

        var read = MapStateSerializer.Read(MapStateSerializer.Write(state));

        read.Status.Should().Be(MapStateReadStatus.Ok);
        read.State.FogZones.Should().BeEquivalentTo(
            [new MapFogZone("a", [0, 0.1, 10, 0, 10, 10.3]), new MapFogZone("b", Square, true)],
            o => o.WithStrictOrdering());
    }

    [Fact]
    public void Version_2_file_without_fog_reads_ok_with_no_zones()
    {
        var result = MapStateSerializer.Read("""{"version":2,"tokens":[]}""");

        result.Status.Should().Be(MapStateReadStatus.Ok);
        result.State.FogZones.Should().BeEmpty();
    }

    [Fact]
    public void Version_4_file_with_fog_is_future_version()
    {
        MapStateSerializer.Read("""{"version":4,"fog":[{"id":"z","points":[0,0,1,0,1,1]}]}""").Status
            .Should().Be(MapStateReadStatus.FutureVersion);
    }

    [Theory]
    [InlineData("""{"version":3,"fog":[{"id":"z","points":[0,0,1,0,1]}]}""")]
    [InlineData("""{"version":3,"fog":[{"points":[0,0,1,0,1,1]}]}""")]
    public void Fog_zone_with_odd_points_or_without_id_is_invalid(string json)
    {
        MapStateSerializer.Read(json).Status.Should().Be(MapStateReadStatus.Invalid);
    }
}
