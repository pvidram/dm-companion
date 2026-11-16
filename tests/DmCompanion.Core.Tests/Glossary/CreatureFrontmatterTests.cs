using DmCompanion.Core.GameSystems;
using DmCompanion.Core.Glossary;
using Microsoft.Extensions.Logging.Abstractions;
using DmCompanion.Core.Vault;

namespace DmCompanion.Core.Tests.Glossary;

public sealed class CreatureFrontmatterTests
{
    private static CreatureStats Creature(string key) => new GlossaryPackLoader(new FixturePackSource(), NullLogger<GlossaryPackLoader>.Instance).Get(key)!.Creature!;

    [Fact]
    public void Aboleth_evaluates_to_ac_17_hp_150_and_initiative_plus_7()
    {
        var values = new Dnd5eSystem().Evaluate(CreatureFrontmatter.From(Creature("srd:criaturas/aboleth")));
        (values.ById["ac"].Display + "|" + values.ById["hp"].Display + "|" + values.ById["initiative"].Display).Should().Be("17|150|+7");
    }

    [Fact]
    public void Values_are_strings()
    {
        var stats = FrontmatterReader.GetMap(CreatureFrontmatter.From(Creature("srd:criaturas/aboleth")), "stats");
        stats["ac"].Should().Be("17");
    }

    [Fact]
    public void Saves_only_where_they_differ()
    {
        var stats = FrontmatterReader.GetMap(CreatureFrontmatter.From(Creature("srd:criaturas/aboleth")), "stats");
        FrontmatterReader.GetMap(stats, "saves").Keys.Should().BeEquivalentTo("dex", "con", "int", "wis");
    }

    [Fact]
    public void Fractional_cr_gives_proficiency_from_the_table()
    {
        var stats = FrontmatterReader.GetMap(CreatureFrontmatter.From(Creature("srd:criaturas/goblin")), "stats");
        ProficiencyBonus.Resolve(stats).Value.Should().Be(2);
    }
}
