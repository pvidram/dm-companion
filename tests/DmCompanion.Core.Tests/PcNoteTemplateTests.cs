using DmCompanion.Core.GameSystems;
using DmCompanion.Core.Vault;

namespace DmCompanion.Core.Tests;

public class PcNoteTemplateTests
{
    private static readonly IReadOnlyDictionary<string, int> NoStats = new Dictionary<string, int>();

    private static VaultEntity Read(PcDraft draft, string systemId = "dnd5e") =>
        VaultEntityFactory.Create("Personajes/x.md", NoteTemplates.CreatePc(draft, GameSystemRegistry.Default.Get(systemId)), DateTime.UnixEpoch);

    [Fact]
    public void Pc_note_round_trips_tricky_text()
    {
        var entity = Read(new PcDraft("Ñandú \"el Bravo\": #1 \\ fin [x]", "O'Neil: {y} - z", "José Mª", NoStats));

        (entity.Title, entity.Alias, entity.Player, entity.ParseError)
            .Should().Be(("Ñandú \"el Bravo\": #1 \\ fin [x]", "O'Neil: {y} - z", "José Mª", null));
    }

    [Fact]
    public void Pc_note_collapses_line_breaks_in_text_fields() =>
        Read(new PcDraft("A\nB\nC", null, null, NoStats)).Title.Should().Be("A B C");

    [Fact]
    public void Pc_note_omits_alias_and_player_when_blank()
    {
        var entity = Read(new PcDraft("Mira", null, "  ", NoStats));

        entity.Frontmatter.Keys.Should().NotContain(["alias", "player"]);
    }

    [Fact]
    public void Pc_note_is_a_pc_with_the_template_body()
    {
        var entity = Read(new PcDraft("Mira", null, null, NoStats));

        (entity.Type, entity.Body.Contains("## Trasfondo")).Should().Be((EntityType.Pc, true));
    }

    [Fact]
    public void Pc_note_stats_are_evaluated_by_the_system()
    {
        var entity = Read(new PcDraft("Mira", null, null, new Dictionary<string, int> { ["ac"] = 16 }));

        GameSystemRegistry.Default.Get("dnd5e").Evaluate(entity.Frontmatter).ById["ac"].Display.Should().Be("16");
    }

    [Theory]
    [MemberData(nameof(SystemIds))]
    public void Pc_note_parses_without_warnings_in_every_system(string systemId)
    {
        var system = GameSystemRegistry.Default.Get(systemId);
        var entity = Read(new PcDraft("Mira", "Cuervo", "Ana", CreationField.Defaults(system.PcCreationFields)), systemId);

        entity.ParseError.Should().BeNull();
    }

    public static TheoryData<string> SystemIds() => new(GameSystemRegistry.Default.All.Select(s => s.Id));

    [Fact]
    public void Validate_reports_blank_name() =>
        new PcDraft(" ", null, null, NoStats).Validate(GameSystemRegistry.Default.Get("dnd5e")).Should().Contain("Escribe un nombre.");

    [Fact]
    public void Validate_reports_out_of_range_stat() =>
        new PcDraft("Mira", null, null, new Dictionary<string, int> { ["str"] = 31 })
            .Validate(GameSystemRegistry.Default.Get("dnd5e")).Should().ContainSingle().Which.Should().Contain("Fuerza");

    [Fact]
    public void Validate_is_empty_for_defaults()
    {
        var system = GameSystemRegistry.Default.Get("dnd5e");

        new PcDraft("Mira", null, null, CreationField.Defaults(system.PcCreationFields)).Validate(system).Should().BeEmpty();
    }
}
