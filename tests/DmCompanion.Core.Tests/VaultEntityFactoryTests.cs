using DmCompanion.Core.Vault;

namespace DmCompanion.Core.Tests;

public class VaultEntityFactoryTests
{
    private static readonly DateTime Now = new(2026, 11, 3, 10, 0, 0, DateTimeKind.Utc);

    private static VaultEntity Create(string content, string path = "Personajes/Varek.md") =>
        VaultEntityFactory.Create(path, content, Now);

    [Fact]
    public void Create_takes_title_from_name_field() =>
        Create("---\nname: Varek el Tuerto\n---\n").Title.Should().Be("Varek el Tuerto");

    [Fact]
    public void Create_falls_back_to_file_name_for_title() =>
        Create("Sin frontmatter").Title.Should().Be("Varek");

    [Fact]
    public void Alias_and_player_read_from_frontmatter()
    {
        var entity = Create("---\ntype: pc\nalias: Cuervo\nplayer: Ana\n---\n");

        (entity.Alias, entity.Player).Should().Be(("Cuervo", "Ana"));
    }

    [Fact]
    public void Alias_and_player_are_null_when_absent()
    {
        var entity = Create("---\ntype: pc\n---\n");

        (entity.Alias, entity.Player).Should().Be((null, null));
    }

    [Fact]
    public void Create_reads_type() =>
        Create("---\ntype: npc\n---\n").Type.Should().Be(EntityType.Npc);

    [Fact]
    public void Create_extracts_links_from_body() =>
        Create("---\ntype: npc\n---\nVive en [[Puerto Sal]].").Links.Single().Target.Should().Be("Puerto Sal");

    [Fact]
    public void Create_keeps_parse_error() =>
        Create("---\nname: [roto\n---\nTexto").ParseError.Should().NotBeNull();

    [Fact]
    public void Create_normalises_path() =>
        Create("x", @"Personajes\Varek.md").RelativePath.Should().Be("Personajes/Varek.md");

    [Fact]
    public void Initial_is_uppercase_first_letter_of_title() =>
        Create("---\nname: ysolde\n---\n").Initial.Should().Be("Y");

    [Fact]
    public void Portrait_reads_frontmatter() =>
        Create("---\nportrait: _assets/varek.png\n---\n").Portrait.Should().Be("_assets/varek.png");

    [Fact]
    public void IsSecret_reads_frontmatter() =>
        Create("---\nsecret: true\n---\n").IsSecret.Should().BeTrue();

    [Fact]
    public void IsSecret_is_true_when_frontmatter_is_invalid() =>
        Create("---\nsecret: true\nname: [roto\n---\nTexto").IsSecret.Should().BeTrue();
}
