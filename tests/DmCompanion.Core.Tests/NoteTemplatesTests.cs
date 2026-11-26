using DmCompanion.Core.GameSystems;
using DmCompanion.Core.Vault;

namespace DmCompanion.Core.Tests;

public class NoteTemplatesTests
{
    private static readonly IGameSystem Dnd5e = GameSystemRegistry.Default.Get("dnd5e");

    // Plantilla de personaje jugador anterior a los sistemas de juego (commit de6f828), sin initiative_bonus: la iniciativa se deriva de la Destreza.
    private const string LegacyPcTemplate =
        "---\ntype: pc\nname: \"Lyra\"\n" +
        "portrait:\nstats:\n  ac: 10\n  hp: 10\n  speed: 30\n" +
        "  abilities: { str: 10, dex: 10, con: 10, int: 10, wis: 10, cha: 10 }\n";

    [Theory]
    [InlineData(EntityType.Pc)]
    [InlineData(EntityType.Npc)]
    [InlineData(EntityType.Location)]
    [InlineData(EntityType.Note)]
    [InlineData(EntityType.Session)]
    public void Create_produces_note_with_matching_type(EntityType type)
    {
        var entity = VaultEntityFactory.Create("x.md", NoteTemplates.Create(type, "Prueba", Dnd5e), DateTime.UnixEpoch);
        (entity.Type, entity.ParseError).Should().Be((type, (string?)null));
    }

    [Fact]
    public void Create_map_has_type_and_name_without_image()
    {
        var entity = VaultEntityFactory.Create("Mapas/x.md", NoteTemplates.Create(EntityType.Map, "Cripta", Dnd5e), DateTime.UnixEpoch);
        (entity.Type, entity.Title, entity.ParseError).Should().Be((EntityType.Map, "Cripta", (string?)null));
        entity.Frontmatter.Should().NotContainKey("image");
    }

    [Fact]
    public void Create_map_includes_image_when_given()
    {
        var entity = VaultEntityFactory.Create("Mapas/x.md", NoteTemplates.Create(EntityType.Map, "Cripta", Dnd5e, "Mapas/Cripta.png"), DateTime.UnixEpoch);
        entity.Frontmatter["image"].Should().Be("Mapas/Cripta.png");
    }

    [Fact]
    public void Create_escapes_title_with_quotes()
    {
        var content = NoteTemplates.Create(EntityType.Note, "El \"Kraken\": inicio", Dnd5e);
        VaultEntityFactory.Create("x.md", content, DateTime.UnixEpoch).Title.Should().Be("El \"Kraken\": inicio");
    }

    [Fact]
    public void Create_adds_stats_for_characters()
    {
        var fields = FrontmatterParser.Parse(NoteTemplates.Create(EntityType.Pc, "Lyra", Dnd5e)).Fields;
        Dnd5e.Evaluate(fields).ById["ac"].Number.Should().Be(10);
    }

    [Fact]
    public void Create_dnd5e_pc_keeps_the_legacy_template_without_initiative_bonus_as_exact_prefix() =>
        NoteTemplates.Create(EntityType.Pc, "Lyra", Dnd5e).Should().StartWith(LegacyPcTemplate);

    [Fact]
    public void Create_dnd5e_npc_adds_secret_before_the_stats() =>
        NoteTemplates.Create(EntityType.Npc, "Olm", Dnd5e).Should().Contain("name: \"Olm\"\nsecret: false\nportrait:\nstats:\n");

    [Fact]
    public void Create_cosmere_pc_generates_its_own_keys() =>
        NoteTemplates.Create(EntityType.Pc, "Kal", GameSystemRegistry.Default.Get("cosmere"))
            .Should().Contain("attributes:").And.Contain("deflect: 0").And.NotContain("abilities:");

    [Fact]
    public void Create_unknown_system_pc_generates_empty_stats() =>
        NoteTemplates.Create(EntityType.Pc, "X", GameSystemRegistry.Default.Get("homebrew"))
            .Should().Contain("stats: {}\n").And.EndWith("---\n## Trasfondo\n\n## Notas\n");

    [Fact]
    public void Create_campaign_has_minimal_frontmatter_and_summary() =>
        NoteTemplates.Create(EntityType.Campaign, "Mi campaña", GameSystemRegistry.Default.Get("cosmere"))
            .Should().Be("---\ntype: campaign\nname: \"Mi campaña\"\nsystem: cosmere\n---\n## Resumen\n");

    [Fact]
    public void Create_campaign_with_dnd5e_keeps_system_dnd5e() =>
        NoteTemplates.Create(EntityType.Campaign, "Mi campaña", Dnd5e)
            .Should().Contain("system: dnd5e\n");

    [Fact]
    public void DefaultFolder_of_campaign_is_the_vault_root() =>
        NoteTemplates.DefaultFolder(EntityType.Campaign).Should().BeEmpty();

    [Theory]
    [InlineData(EntityType.Pc, "Personajes")]
    [InlineData(EntityType.Npc, "Personajes")]
    [InlineData(EntityType.Location, "Lugares")]
    [InlineData(EntityType.Session, "Historia")]
    [InlineData(EntityType.Note, "Historia")]
    [InlineData(EntityType.Map, "Mapas")]
    [InlineData(EntityType.Monster, "Glosario")]
    public void DefaultFolder_follows_vault_convention(EntityType type, string folder) =>
        NoteTemplates.DefaultFolder(type).Should().Be(folder);

    [Theory]
    [InlineData("Puerto Sal", "Puerto Sal")]
    [InlineData("¿Quién es: el/la [[Kraken]]?", "¿Quién es ella Kraken")]
    [InlineData("  Nota.  ", "Nota")]
    [InlineData("///", "Sin título")]
    [InlineData(".Secreto", "Secreto")]
    [InlineData("..", "Sin título")]
    public void FileName_removes_characters_invalid_in_obsidian(string title, string expected) =>
        NoteFileName.FromTitle(title).Should().Be(expected);
}
