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
        var result = MapStateSerializer.Read(Example.Replace("\"version\": 1", "\"version\": 2"));

        result.Status.Should().Be(MapStateReadStatus.FutureVersion);
        result.State.Version.Should().Be(2);
        result.State.Tokens.Should().HaveCount(2);
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
            1,
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
}
