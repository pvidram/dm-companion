using DmCompanion.Core.GameSystems;

namespace DmCompanion.Core.Tests.GameSystems;

public class CreationFieldTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(30)]
    public void Check_returns_null_inside_range(int value) =>
        new CreationField("str", "Fuerza", 10, 1, 30).Check(value).Should().BeNull();

    [Fact]
    public void Check_names_the_field_and_the_range_outside() =>
        new CreationField("str", "Fuerza", 10, 1, 30).Check(31).Should().Be("Fuerza: debe estar entre 1 y 30.");

    [Fact]
    public void Defaults_maps_each_id_to_its_default() =>
        CreationField.Defaults([new("ac", "CA", 10, 0, 50), new("hp", "PG", 7, 1, 999)])
            .Should().BeEquivalentTo(new Dictionary<string, int> { ["ac"] = 10, ["hp"] = 7 });
}
