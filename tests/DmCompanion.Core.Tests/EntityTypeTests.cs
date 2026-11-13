using DmCompanion.Core.Vault;

namespace DmCompanion.Core.Tests;

public class EntityTypeTests
{
    [Theory]
    [InlineData("pc", EntityType.Pc)]
    [InlineData("NPC", EntityType.Npc)]
    [InlineData(" location ", EntityType.Location)]
    [InlineData("session", EntityType.Session)]
    [InlineData("map", EntityType.Map)]
    [InlineData("rule", EntityType.Rule)]
    [InlineData("monster", EntityType.Monster)]
    [InlineData("campaign", EntityType.Campaign)]
    [InlineData("note", EntityType.Note)]
    public void Parse_recognises_known_types_ignoring_case_and_spaces(string raw, EntityType expected) =>
        EntityTypes.Parse(raw).Should().Be(expected);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("dragon")]
    public void Parse_falls_back_to_note_for_missing_or_unknown_types(string? raw) =>
        EntityTypes.Parse(raw).Should().Be(EntityType.Note);

    [Fact]
    public void ToKey_round_trips_through_parse()
    {
        foreach (var type in Enum.GetValues<EntityType>())
            EntityTypes.Parse(EntityTypes.ToKey(type)).Should().Be(type);
    }
}
