using DmCompanion.Core.Glossary;
using DmCompanion.Core.Vault;
using Microsoft.Extensions.Logging.Abstractions;

namespace DmCompanion.Core.Tests.Glossary;

public class GlossaryIndexTests
{
    private static readonly IReadOnlyList<GlossaryListing> Srd =
        new GlossaryPackLoader(new FixturePackSource(), NullLogger<GlossaryPackLoader>.Instance).Listings().Value;

    private static VaultIndex Vault(params (string Path, string Content)[] notes)
        => VaultIndex.Build(notes.Select(n => VaultEntityFactory.Create(n.Path, n.Content, DateTime.UnixEpoch)));

    private static GlossaryIndex Index(params (string Path, string Content)[] notes) => GlossaryIndex.Build(Srd, Vault(notes));

    private static GlossaryListing Row(string key, GlossaryCategory category, string name, string? nameEn = null, GlossaryFacets? facets = null)
        => new(key, category, null, name, nameEn, "", facets ?? new GlossaryFacets());

    private static string[] Keys(IEnumerable<GlossaryListing> rows) => rows.Select(r => r.Key).ToArray();

    [Fact]
    public void Search_by_spanish_name_ignores_case_and_accents()
        => Keys(Index().Search(new GlossaryQuery("BOLA DE FUÉGO"))).Should().Equal("srd:conjuros/fireball");

    [Fact]
    public void Search_by_english_name()
        => Keys(Index().Search(new GlossaryQuery("fireball"))).Should().Equal("srd:conjuros/fireball");

    [Fact]
    public void Exact_match_ranks_first()
    {
        var srd = new[]
        {
            Row("srd:reglas/a", GlossaryCategory.Rule, "Ataque cegado a ciegas"),
            Row("srd:estados/blinded", GlossaryCategory.Condition, "Cegado", "Blinded"),
            Row("srd:reglas/b", GlossaryCategory.Rule, "Cegado parcial"),
        };
        Keys(GlossaryIndex.Build(srd, VaultIndex.Empty).Search(new GlossaryQuery("cegado")))
            .Should().Equal("srd:estados/blinded", "srd:reglas/b", "srd:reglas/a");
    }

    [Fact]
    public void Category_filter_limits_results()
        => Index().Search(new GlossaryQuery(Category: GlossaryCategory.Condition)).Select(r => r.Category)
            .Should().OnlyContain(c => c == GlossaryCategory.Condition);

    [Fact]
    public void Cr_range_filter_uses_fractions()
        => Keys(Index().Search(new GlossaryQuery(Filter: new GlossaryFilter(CrMin: 0, CrMax: 1))))
            .Should().Equal("srd:criaturas/goblin");

    [Fact]
    public void Cr_filter_of_zero_excludes_rows_without_cr()
        => Index().Search(new GlossaryQuery(Filter: new GlossaryFilter(CrMin: 0, CrMax: 0))).Should().BeEmpty();

    [Fact]
    public void Creature_type_filter()
        => Keys(Index().Search(new GlossaryQuery(Filter: new GlossaryFilter(CreatureType: "Humanoid"))))
            .Should().Equal("srd:criaturas/goblin");

    [Fact]
    public void Spell_class_filter()
        => Keys(Index().Search(new GlossaryQuery(Filter: new GlossaryFilter(SpellClass: "Wizard"))))
            .Should().Equal("srd:conjuros/fireball");

    [Fact]
    public void Rarity_filter()
        => Keys(Index().Search(new GlossaryQuery(Filter: new GlossaryFilter(Rarity: "Uncommon"))))
            .Should().Equal("srd:objetos/adamantine-armor");

    [Fact]
    public void Vault_rule_with_srd_source_overrides_a_spell()
    {
        var index = Index(("Reglas/Bola de fuego.md", "---\ntype: rule\nname: Bola de fuego\nsource: \"srd:conjuros/fireball\"\n---\n"));

        var row = index.Search(new GlossaryQuery("bola de fuego")).Should().ContainSingle().Subject;
        (row.IsVault, row.Overrides, index.OverriddenBy("srd:conjuros/fireball")?.Key, index.Get("srd:conjuros/fireball")?.Key,
                index.TryResolve("bola de fuego"))
            .Should().Be((true, "srd:conjuros/fireball", row.Key, "srd:conjuros/fireball", null));
    }

    [Fact]
    public void Unknown_source_falls_back_to_name_matching()
    {
        var index = Index(("Glosario/Aboleth.md", "---\ntype: monster\nname: Aboleth\nsource: \"srd:criaturas/nope\"\n---\n"));

        index.Search(new GlossaryQuery("aboleth")).Should().ContainSingle().Which.Overrides.Should().Be("srd:criaturas/aboleth");
    }

    [Fact]
    public void Vault_monster_overrides_srd_creature_with_the_same_name()
    {
        var result = Index(("Glosario/Aboleth.md", "---\ntype: monster\nstats:\n  cr: 11\n---\n")).Search(new GlossaryQuery("aboleth"));
        var row = result.Should().ContainSingle().Subject;
        (row.IsVault, row.Overrides, row.Summary, row.Facets.Cr).Should().Be((true, "srd:criaturas/aboleth", "VD 11 · Tu nota", "11"));
    }

    [Fact]
    public void Vault_monster_without_cr_says_only_your_note()
        => Index(("Glosario/Rarito.md", "---\ntype: monster\n---\n")).Get("vault:Glosario/Rarito.md")!.Summary.Should().Be("Tu nota");

    [Fact]
    public void Overridden_srd_entry_is_still_reachable_by_key()
    {
        var index = Index(("Glosario/Aboleth.md", "---\ntype: monster\n---\n"));
        (index.Get("srd:criaturas/aboleth")!.IsVault, index.OverriddenBy("srd:criaturas/aboleth")!.Key)
            .Should().Be((false, "vault:Glosario/Aboleth.md"));
    }

    [Fact]
    public void Vault_rule_does_not_override_a_creature()
    {
        var index = Index(("Glosario/Aboleth.md", "---\ntype: rule\n---\n"));
        (index.Search(new GlossaryQuery("aboleth")).Count, index.OverriddenBy("srd:criaturas/aboleth")).Should().Be((2, null));
    }

    [Fact]
    public void Secret_vault_notes_are_included()
        => Index(("Glosario/Oculto.md", "---\ntype: rule\nsecret: true\n---\n")).Get("vault:Glosario/Oculto.md").Should().NotBeNull();

    [Fact]
    public void TryResolve_prefers_conditions_over_creatures()
    {
        var srd = Srd.Append(Row("srd:criaturas/cegado", GlossaryCategory.Creature, "Cegado")).ToList();
        GlossaryIndex.Build(srd, VaultIndex.Empty).TryResolve("cegado")!.Key.Should().Be("srd:estados/blinded");
    }

    [Fact]
    public void TryResolve_ignores_overridden_entries()
        => Index(("Glosario/Aboleth.md", "---\ntype: monster\n---\n")).TryResolve("Aboleth").Should().BeNull();

    [Fact]
    public void Empty_srd_list_gives_only_vault_entries()
    {
        var index = GlossaryIndex.Build([], Vault(("Glosario/Regla.md", "---\ntype: rule\n---\n")));
        Keys(index.Search(new GlossaryQuery())).Should().Equal("vault:Glosario/Regla.md");
    }

    [Fact]
    public void Search_caps_at_200()
    {
        var srd = Enumerable.Range(0, 250).Select(i => Row($"srd:reglas/r{i:D3}", GlossaryCategory.Rule, $"Regla {i:D3}")).ToList();
        GlossaryIndex.Build(srd, VaultIndex.Empty).Search(new GlossaryQuery()).Should().HaveCount(200);
    }

    [Fact]
    public void ConditionKeys_lists_srd_conditions()
        => Index().ConditionKeys.Should().BeEquivalentTo("srd:estados/blinded", "srd:estados/prone");

    [Fact]
    public void Normalize_strips_diacritics_and_case()
        => GlossaryText.Normalize("Ñandú").Should().Be("nandu");
}
