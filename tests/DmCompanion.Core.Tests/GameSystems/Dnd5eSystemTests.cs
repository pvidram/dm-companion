using System.Globalization;
using DmCompanion.Core.GameSystems;
using DmCompanion.Core.Vault;

namespace DmCompanion.Core.Tests.GameSystems;

public class Dnd5eSystemTests
{
    private static readonly Dnd5eSystem System = new();

    private static StatValues Eval(string yaml) =>
        System.Evaluate(FrontmatterParser.Parse("---\n" + yaml + "\n---\n").Fields);

    private static StatValues EvalWith(string stats, string abilities = "{ str: 10, dex: 10, con: 10, int: 10, wis: 10, cha: 10 }") =>
        Eval("stats:\n  abilities: " + abilities + "\n" + stats);

    private static readonly string LyraYaml = """
        stats:
          ac: 14
          hp: 27
          speed: 30
          initiative_bonus: 3
          abilities: { str: 8, dex: 16, con: 12, int: 13, wis: 14, cha: 10 }
        """;

    // --- Lyra (ficha de hoy) ---
    [Fact] public void Lyra_ac_is_14() => Eval(LyraYaml).ById["ac"].Display.Should().Be("14");
    [Fact] public void Lyra_hp_is_27() => Eval(LyraYaml).ById["hp"].Display.Should().Be("27");
    [Fact] public void Lyra_initiative_is_signed() => Eval(LyraYaml).ById["initiative"].Display.Should().Be("+3");
    [Fact] public void Lyra_str_shows_score_and_modifier() => Eval(LyraYaml).ById["str"].Display.Should().Be("8 (−1)");

    // --- Bonificador de competencia ---
    private static string PerceptionProficient(string extra) =>
        Eval("stats:\n  abilities: { wis: 10 }\n  skills: { perception: proficient }\n" + extra).ById["skill_perception"].Display;

    [Theory]
    [InlineData("1", "+2")]
    [InlineData("4", "+2")]
    [InlineData("5", "+3")]
    [InlineData("17", "+6")]
    public void Proficiency_from_level(string level, string expected) =>
        PerceptionProficient("  level: " + level).Should().Be(expected);

    [Theory]
    [InlineData("\"1/4\"", "+2")]
    [InlineData("5", "+3")]
    [InlineData("30", "+9")]
    public void Proficiency_from_cr(string cr, string expected) =>
        PerceptionProficient("  cr: " + cr).Should().Be(expected);

    [Fact]
    public void Explicit_proficiency_bonus_beats_level() =>
        PerceptionProficient("  level: 1\n  proficiency_bonus: 5").Should().Be("+5");

    [Fact]
    public void Missing_proficiency_shows_dash() => PerceptionProficient("").Should().Be("—");

    [Fact]
    public void Missing_proficiency_has_reason_and_missing_source()
    {
        var v = Eval("stats:\n  abilities: { wis: 10 }\n  skills: { perception: proficient }").ById["skill_perception"];
        (v.Source, v.Reason).Should().Be((StatSource.Missing, "Falta el bonificador de competencia (level, cr o proficiency_bonus)."));
    }

    [Fact]
    public void ProficiencyBonus_resolve_without_data_returns_reason() =>
        ProficiencyBonus.Resolve(new Dictionary<string, object?>()).Should()
            .Be((null, "Falta el bonificador de competencia (level, cr o proficiency_bonus)."));

    // --- Habilidades y salvaciones ---
    [Fact]
    public void Expertise_doubles_proficiency() =>
        EvalWith("  level: 1\n  skills: { stealth: expertise }", "{ dex: 16 }").ById["skill_stealth"].Display.Should().Be("+7");

    [Fact]
    public void No_proficiency_gives_only_modifier() =>
        EvalWith("  level: 5\n  skills: { stealth: none }", "{ dex: 16 }").ById["skill_stealth"].Display.Should().Be("+3");

    [Fact]
    public void Missing_skill_counts_as_none_without_warning()
    {
        var r = EvalWith("  level: 5", "{ dex: 16 }");
        (r.ById["skill_stealth"].Display, r.Warnings.Count).Should().Be(("+3", 0));
    }

    [Fact]
    public void Proficiency_text_is_case_insensitive() =>
        EvalWith("  level: 1\n  skills: { stealth: Proficient }", "{ dex: 10 }").ById["skill_stealth"].Display.Should().Be("+2");

    [Fact]
    public void Proficient_save_adds_proficiency() =>
        EvalWith("  level: 1\n  saves: { dex: proficient }", "{ dex: 14 }").ById["save_dex"].Display.Should().Be("+4");

    [Fact]
    public void Missing_ability_gives_dash_with_reason()
    {
        var v = Eval("stats:\n  level: 1\n  skills: { stealth: none }").ById["skill_stealth"];
        (v.Display, v.Source, v.Reason).Should().Be(("—", StatSource.Missing, "Falta la característica."));
    }

    [Fact]
    public void Manual_number_has_manual_source() =>
        EvalWith("  skills: { stealth: 9 }").ById["skill_stealth"].Source.Should().Be(StatSource.Manual);

    [Fact]
    public void Manual_number_is_shown_signed() =>
        EvalWith("  skills: { stealth: 9 }").ById["skill_stealth"].Display.Should().Be("+9");

    [Fact]
    public void Unknown_value_is_treated_as_none() =>
        EvalWith("  level: 5\n  skills: { stealth: \"pro\" }", "{ dex: 16 }").ById["skill_stealth"].Display.Should().Be("+3");

    [Fact]
    public void Unknown_value_warns_without_repeating_content() =>
        EvalWith("  skills: { stealth: \"pro\" }").Warnings.Should()
            .Equal("stats.skills.stealth: valor desconocido; se trata como none.");

    // --- Percepción pasiva ---
    [Fact]
    public void Passive_perception_is_10_plus_perception() =>
        EvalWith("  level: 1\n  skills: { perception: proficient }", "{ wis: 14 }").ById["passive_perception"].Display.Should().Be("14");

    [Fact]
    public void Passive_perception_manual_wins() =>
        EvalWith("  passive_perception: 17", "{ wis: 14 }").ById["passive_perception"].Source.Should().Be(StatSource.Manual);

    [Fact]
    public void Passive_perception_manual_is_unsigned() =>
        EvalWith("  passive_perception: 17").ById["passive_perception"].Display.Should().Be("17");

    [Fact]
    public void Passive_perception_is_dash_when_perception_is_dash() =>
        Eval("stats:\n  level: 1").ById["passive_perception"].Display.Should().Be("—");

    // --- Definición ---
    private static IEnumerable<StatDefinition> Group(string id) => System.Definition.Stats.Where(s => s.GroupId == id);

    [Fact]
    public void Skills_group_is_alphabetical() =>
        System.Definition.Groups.Single(g => g.Id == "skills").Order.Should().Be(GroupOrder.Alphabetical);

    [Fact]
    public void Skills_sort_alphabetically_in_spanish() =>
        Group("skills").Select(s => s.Label).Order(StringComparer.Create(new CultureInfo("es-ES"), true)).Take(3)
            .Should().Equal("Acrobacias", "Arcanos", "Atletismo");

    [Fact]
    public void Skills_group_has_18_skills_plus_passive() => Group("skills").Count().Should().Be(19);

    [Fact]
    public void Skill_carries_ability_abbreviation() =>
        Group("skills").Single(s => s.Id == "skill_stealth").Abbreviation.Should().Be("DES");

    [Fact]
    public void Quick_card_stats_are_ac_hp_speed_and_passive() =>
        System.Definition.Stats.Where(s => s.ShowInQuickCard).Select(s => s.Id)
            .Should().BeEquivalentTo("ac", "hp", "speed", "passive_perception");

    // --- Plantilla ---
    private const string TodayTemplate = "portrait:\nstats:\n  ac: 10\n  hp: 10\n  speed: 30\n  initiative_bonus: 0\n  abilities: { str: 10, dex: 10, con: 10, int: 10, wis: 10, cha: 10 }\n";

    [Fact]
    public void Template_starts_with_todays_template() =>
        System.StatsTemplate(EntityType.Pc).Should().StartWith(TodayTemplate);

    [Fact]
    public void Template_contains_18_skills_set_to_none() =>
        Dnd5eSkills.All.Select(s => "    " + s.Key + ": none\n").Should().OnlyContain(l => System.StatsTemplate(EntityType.Pc).Contains(l));

    [Fact]
    public void Template_has_six_saves_set_to_none() =>
        System.StatsTemplate(EntityType.Pc).Should().Contain("  saves: { str: none, dex: none, con: none, int: none, wis: none, cha: none }\n");

    [Fact]
    public void Template_has_level_1() => System.StatsTemplate(EntityType.Pc).Should().Contain("  level: 1\n");

    [Fact]
    public void Npc_template_equals_pc_template() =>
        System.StatsTemplate(EntityType.Npc).Should().Be(System.StatsTemplate(EntityType.Pc));

    [Fact]
    public void Template_parses_and_evaluates_without_warnings() =>
        Eval(System.StatsTemplate(EntityType.Pc)).Warnings.Should().BeEmpty();

    [Fact] public void HitPoints_max_is_stats_hp() =>
        System.HitPoints.Max(Eval("stats:\n  hp: 27")).Should().Be(27);

    [Fact] public void HitPoints_max_is_null_without_hp() =>
        System.HitPoints.Max(Eval("stats:\n  ac: 14")).Should().BeNull();

    [Fact] public void HitPoints_label_is_HP_and_replaces_the_hp_stat() =>
        (System.HitPoints.Label, System.HitPoints.StatId).Should().Be(("HP", "hp"));
}
