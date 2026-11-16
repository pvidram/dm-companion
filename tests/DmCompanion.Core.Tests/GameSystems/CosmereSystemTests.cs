using DmCompanion.Core.GameSystems;
using DmCompanion.Core.Vault;

namespace DmCompanion.Core.Tests.GameSystems;

public class CosmereSystemTests
{
    private static readonly CosmereSystem System = new();

    private static StatValues Eval(string yaml) =>
        System.Evaluate(FrontmatterParser.Parse("---\n" + yaml + "\n---\n").Fields);

    private const string Attributes = "  attributes: { str: 2, spd: 3, int: 1, wil: 2, awa: 0, pre: 1 }\n";

    [Fact] public void Physical_defense_is_10_plus_str_plus_spd() =>
        Eval("stats:\n" + Attributes).ById["physical"].Display.Should().Be("15");

    [Fact] public void Cognitive_defense_is_10_plus_int_plus_wil() =>
        Eval("stats:\n" + Attributes).ById["cognitive"].Display.Should().Be("13");

    [Fact] public void Spiritual_defense_is_10_plus_awa_plus_pre() =>
        Eval("stats:\n" + Attributes).ById["spiritual"].Display.Should().Be("11");

    [Fact] public void Manual_defense_overrides_derived() =>
        Eval("stats:\n" + Attributes + "  defenses: { physical: 18 }\n").ById["physical"].Display.Should().Be("18");

    [Fact] public void Manual_defense_is_marked_manual() =>
        Eval("stats:\n" + Attributes + "  defenses: { physical: 18 }\n").ById["physical"].Source.Should().Be(StatSource.Manual);

    [Fact] public void Defense_without_attribute_is_dash() =>
        Eval("stats:\n  attributes: { str: 2 }\n").ById["physical"].Display.Should().Be("—");

    [Fact] public void Defense_without_attribute_explains_reason() =>
        Eval("stats:\n  attributes: { str: 2 }\n").ById["physical"].Reason.Should().Be("Falta el atributo.");

    [Fact] public void Resource_as_map_shows_current_over_max() =>
        Eval("stats:\n  health: { current: 12, max: 15 }\n").ById["health"].Display.Should().Be("12/15");

    [Fact] public void Resource_as_number_shows_max_only() =>
        Eval("stats:\n  focus: 15\n").ById["focus"].Display.Should().Be("15");

    [Fact] public void Missing_resource_is_dash() =>
        Eval("stats: {}\n").ById["investiture"].Display.Should().Be("—");

    [Fact] public void Deflect_is_plain_number() =>
        Eval("stats:\n  deflect: 2\n").ById["deflect"].Display.Should().Be("2");

    [Fact] public void Skill_is_attribute_plus_rank_signed() =>
        Eval("stats:\n" + Attributes + "  skills: { athletics: 3 }\n").ById["athletics"].Display.Should().Be("+5");

    [Fact] public void Skill_without_rank_counts_rank_as_zero() =>
        Eval("stats:\n" + Attributes).ById["agility"].Display.Should().Be("+3");

    [Fact] public void Skill_without_attribute_is_dash() =>
        Eval("stats:\n  skills: { athletics: 3 }\n").ById["athletics"].Source.Should().Be(StatSource.Missing);

    [Fact] public void Skill_without_attribute_explains_reason() =>
        Eval("stats:\n  skills: { athletics: 3 }\n").ById["athletics"].Reason.Should().Be("Falta el atributo.");

    [Fact] public void Rank_out_of_range_counts_as_zero() =>
        Eval("stats:\n" + Attributes + "  skills: { athletics: 7 }\n").ById["athletics"].Display.Should().Be("+2");

    [Fact] public void Rank_out_of_range_adds_warning() =>
        Eval("stats:\n" + Attributes + "  skills: { athletics: 7 }\n").Warnings.Should().ContainSingle()
            .Which.Should().Contain("stats.skills.athletics");

    [Fact] public void Text_rank_adds_warning_and_counts_as_zero()
    {
        var values = Eval("stats:\n" + Attributes + "  skills: { lore: mucho }\n");
        (values.ById["lore"].Display, values.Warnings.Count).Should().Be(("+1", 1));
    }

    [Fact] public void Warning_does_not_repeat_content_value() =>
        Eval("stats:\n" + Attributes + "  skills: { lore: mucho }\n").Warnings[0].Should().NotContain("mucho");

    [Fact] public void Template_evaluates_without_warnings() =>
        System.Evaluate(FrontmatterParser.Parse("---\n" + System.StatsTemplate(EntityType.Npc) + "---\n").Fields)
            .Warnings.Should().BeEmpty();

    [Fact] public void Template_has_empty_defenses() =>
        System.StatsTemplate(EntityType.Npc).Should().Contain("  defenses: {}\n");

    [Fact] public void Template_has_all_18_skills() =>
        System.StatsTemplate(EntityType.Npc).Split('\n').Count(l => l.StartsWith("    ") && l.EndsWith(": 0")).Should().Be(18);

    [Fact] public void Template_skills_evaluate_to_zero_signed() =>
        System.Evaluate(FrontmatterParser.Parse("---\n" + System.StatsTemplate(EntityType.Npc) + "---\n").Fields)
            .ById["stealth"].Display.Should().Be("+0");

    [Fact] public void No_stat_is_shown_in_quick_card() =>
        System.Definition.Stats.Should().OnlyContain(s => !s.ShowInQuickCard);

    [Fact] public void Skill_abbreviation_is_attribute_label_uppercase() =>
        System.Definition.Stats.Single(s => s.Id == "persuasion").Abbreviation.Should().Be("PRE");

    [Fact] public void Skills_group_is_alphabetical() =>
        System.Definition.Groups.Single(g => g.Id == "skills").Order.Should().Be(GroupOrder.Alphabetical);

    private static IEnumerable<string> IdsIn(string group) =>
        System.Definition.Stats.Where(s => s.GroupId == group).Select(s => s.Id);

    [Fact] public void Deflect_is_in_defenses_group_after_the_three_defenses() =>
        IdsIn("defenses").Should().Equal("physical", "cognitive", "spiritual", "deflect");

    [Fact] public void Movement_is_in_basic_group() =>
        IdsIn("basic").Should().Equal("movement");

    [Fact] public void Resources_group_has_only_health_focus_and_investiture() =>
        IdsIn("resources").Should().Equal("health", "focus", "investiture");

    [Fact] public void First_group_is_basic() =>
        System.Definition.Groups[0].Id.Should().Be("basic");

    [Fact] public void Groups_follow_basic_attributes_defenses_resources_skills() =>
        System.Definition.Groups.Select(g => g.Id).Should().Equal("basic", "attributes", "defenses", "resources", "skills");

    [Fact] public void Deflect_stays_a_number_stat() =>
        System.Definition.Stats.Single(s => s.Id == "deflect").Kind.Should().Be(StatKind.Number);

    [Fact] public void HitPoints_max_is_the_health_maximum() =>
        System.HitPoints.Max(Eval("stats:\n  health: { current: 12, max: 15 }")).Should().Be(15);

    [Fact] public void HitPoints_max_is_null_without_health() =>
        System.HitPoints.Max(Eval("stats:\n  focus: { current: 1, max: 2 }")).Should().BeNull();

    [Fact] public void HitPoints_label_is_Salud() => System.HitPoints.Label.Should().Be("Salud");

    [Fact] public void Cosmere_uses_no_srd_pack() => System.GlossaryPacks.Should().BeEmpty();
}
