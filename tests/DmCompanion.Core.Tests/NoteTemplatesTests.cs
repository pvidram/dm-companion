using DmCompanion.Core.Vault;

namespace DmCompanion.Core.Tests;

public class NoteTemplatesTests
{
    [Theory]
    [InlineData(EntityType.Pc)]
    [InlineData(EntityType.Npc)]
    [InlineData(EntityType.Location)]
    [InlineData(EntityType.Note)]
    [InlineData(EntityType.Session)]
    public void Create_produces_note_with_matching_type(EntityType type)
    {
        var entity = VaultEntityFactory.Create("x.md", NoteTemplates.Create(type, "Prueba"), DateTime.UnixEpoch);
        (entity.Type, entity.ParseError).Should().Be((type, (string?)null));
    }

    [Fact]
    public void Create_escapes_title_with_quotes()
    {
        var content = NoteTemplates.Create(EntityType.Note, "El \"Kraken\": inicio");
        VaultEntityFactory.Create("x.md", content, DateTime.UnixEpoch).Title.Should().Be("El \"Kraken\": inicio");
    }

    [Fact]
    public void Create_adds_stats_for_characters()
    {
        var fields = FrontmatterParser.Parse(NoteTemplates.Create(EntityType.Pc, "Lyra")).Fields;
        CharacterStats.FromFrontmatter(fields).ArmorClass.Should().Be(10);
    }

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
