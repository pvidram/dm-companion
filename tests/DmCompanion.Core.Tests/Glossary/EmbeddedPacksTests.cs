using DmCompanion.Core.GameSystems;
using DmCompanion.Core.Glossary;
using Microsoft.Extensions.Logging.Abstractions;

namespace DmCompanion.Core.Tests.Glossary;

/// <summary>The packs generated from the SRD, read through the same path the app uses.</summary>
public sealed class EmbeddedPacksTests
{
    private static readonly GlossaryPackLoader Loader = new(new EmbeddedGlossaryPackSource(), NullLogger<GlossaryPackLoader>.Instance);

    private static IEnumerable<GlossaryBlock> Flatten(IEnumerable<GlossaryBlock> blocks) =>
        blocks.SelectMany(b => new[] { b }.Concat(Flatten(b.Children ?? [])));

    [Fact]
    public void Every_pack_loads_without_error()
    {
        var errors = GlossaryPacks.All.Select(c => Loader.Entries(c).Error).Append(Loader.Listings().Error);

        errors.Should().AllBeEquivalentTo((string?)null);
    }

    [Fact]
    public void Counts_match_the_source()
    {
        var counts = GlossaryPacks.All.Select(c => Loader.Entries(c).Value.Count).ToList();

        counts.Append(Loader.Listings().Value.Count).Should().Equal(215, 15, 330, 339, 258, 157, 54, 1368);
    }

    [Fact]
    public void Every_state_key_matches_a_condition_of_dnd5e()
    {
        var states = Loader.Entries(GlossaryCategory.Condition).Value.Keys.Select(k => k["srd:estados/".Length..]);
        var conditions = Dnd5eCombatRules.Instance.Conditions.Select(c => c.Key).Where(k => k != "concentration");

        states.Should().BeEquivalentTo(conditions);
    }

    [Fact]
    public void A_real_rule_has_a_table_with_rows_after_the_json_round_trip()
    {
        var tables = Loader.Entries(GlossaryCategory.Rule).Value.Values.SelectMany(e => Flatten(e.Blocks)).Where(b => b.Kind == BlockKind.Table);

        tables.Should().Contain(t => t.Rows != null && t.Rows.Count > 0 && t.Rows[0].Count > 0 && t.Rows[0][0].Count > 0);
    }

    [Fact]
    public void A_real_rule_has_a_box_with_children()
    {
        var boxes = Loader.Entries(GlossaryCategory.Rule).Value.Values.SelectMany(e => Flatten(e.Blocks)).Where(b => b.Kind == BlockKind.Box);

        boxes.Should().Contain(b => b.Children != null && b.Children.Count > 0);
    }

    [Fact]
    public void A_real_spell_embeds_a_stat_block_box()
        => Loader.Get("srd:conjuros/animate-objects")!.Blocks.Should().Contain(b => b.Kind == BlockKind.Box && b.Title == "Objeto animado");

    [Fact]
    public void A_real_creature_keeps_its_numbers_and_sections()
    {
        var aboleth = Loader.Get("srd:criaturas/aboleth")!.Creature!;

        (aboleth.Ac, aboleth.Hp, aboleth.SpeedFeet, aboleth.Sections.Count).Should().Be((17, 150, 10, 3));
    }

    [Fact]
    public void Every_creature_type_line_starts_with_the_label_of_its_type_key()
    {
        var creatures = Loader.Entries(GlossaryCategory.Creature).Value.Values.Select(e => e.Creature!);

        var wrong = creatures.Where(c => !c.TypeLine.StartsWith(GlossaryLabels.CreatureType(c.TypeKey), StringComparison.Ordinal)
                                         && !c.TypeLine.StartsWith("Enjambre", StringComparison.Ordinal));

        wrong.Should().BeEmpty();
    }

    [Fact]
    public void Every_spell_school_class_and_item_rarity_has_a_label()
    {
        var spells = Loader.Entries(GlossaryCategory.Spell).Value.Values.Select(e => e.Spell!).ToList();
        var items = Loader.Entries(GlossaryCategory.MagicItem).Value.Values.Select(e => e.Item!).ToList();

        var untranslated = spells.Select(s => s.SchoolKey).Where(k => GlossaryLabels.School(k) == k)
            .Concat(spells.SelectMany(s => s.ClassKeys).Where(k => GlossaryLabels.SpellClass(k) == k))
            .Concat(items.SelectMany(i => i.RarityKeys).Where(k => GlossaryLabels.Rarity(k) == k));

        untranslated.Should().BeEmpty();
    }

    [Fact]
    public void Spell_headers_start_with_the_school_label()
    {
        var spells = Loader.Entries(GlossaryCategory.Spell).Value.Values.Select(e => e.Spell!);

        spells.Where(s => !s.Header.Contains(GlossaryLabels.School(s.SchoolKey), StringComparison.OrdinalIgnoreCase)).Should().BeEmpty();
    }
}
