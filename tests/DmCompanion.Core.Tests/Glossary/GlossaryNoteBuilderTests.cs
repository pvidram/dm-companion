using DmCompanion.Core.GameSystems;
using DmCompanion.Core.Glossary;
using Microsoft.Extensions.Logging.Abstractions;
using DmCompanion.Core.Vault;

namespace DmCompanion.Core.Tests.Glossary;

public sealed class GlossaryNoteBuilderTests
{
    private static readonly GlossaryPackLoader Loader = new(new FixturePackSource(), NullLogger<GlossaryPackLoader>.Instance);

    private static GlossaryEntry Rule(string name, params GlossaryBlock[] blocks) =>
        new("srd:reglas/x", GlossaryCategory.Rule, null, name, null, 5, blocks);

    [Fact]
    public void Creature_note_reparses_as_monster_with_same_stats()
    {
        var note = GlossaryNoteBuilder.Build(Loader.Get("srd:criaturas/aboleth")!);
        var entity = VaultEntityFactory.Create(note.RelativePath, note.Content, DateTime.UtcNow);
        var values = new Dnd5eSystem().Evaluate(entity.Frontmatter);
        (entity.Type, values.ById["ac"].Display, values.ById["hp"].Display, values.ById["initiative"].Display)
            .Should().Be((EntityType.Monster, "17", "150", "+7"));
    }

    [Fact]
    public void Rule_note_has_rule_type_and_body_text()
    {
        var note = GlossaryNoteBuilder.Build(Rule("Ventaja", new GlossaryBlock(BlockKind.Paragraph, [new GlossarySpan("Tira "), new GlossarySpan("dos", SpanStyle.Bold)])));
        var parsed = FrontmatterParser.Parse(note.Content);
        (note.Type, FrontmatterReader.GetString(parsed.Fields, "type"), parsed.Body).Should().Be(
            (EntityType.Rule, "rule", "\nTira **dos**\n\n*SRD 5.2.1, p. 5. CC-BY 4.0, Wizards of the Coast.*\n"));
    }

    [Fact]
    public void Path_goes_to_Glosario_folder_with_safe_file_name()
        => GlossaryNoteBuilder.Build(Rule("Ataque: a/b?")).RelativePath.Should().Be("Glosario/Ataque ab.md");

    [Fact]
    public void Wikilink_brackets_in_text_are_escaped()
    {
        var note = GlossaryNoteBuilder.Build(Rule("R", new GlossaryBlock(BlockKind.Paragraph, [new GlossarySpan("ver [[Otra]]")])));
        VaultEntityFactory.Create(note.RelativePath, note.Content, DateTime.UtcNow).Links.Should().BeEmpty();
    }

    [Fact]
    public void Pipes_in_table_cells_are_escaped()
    {
        IReadOnlyList<IReadOnlyList<IReadOnlyList<GlossarySpan>>> rows = [[[new("A")], [new("B")]], [[new("x|y")], [new("z")]]];
        GlossaryNoteBuilder.Build(Rule("R", new GlossaryBlock(BlockKind.Table, Rows: rows))).Content.Should().Contain("| x\\|y | z |");
    }
}
