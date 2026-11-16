using System.Text;
using System.Text.Json;
using DmCompanion.Core.Glossary;
using DmCompanion.SrdImport;

namespace DmCompanion.SrdImport.Tests;

public sealed class ImporterTests : IDisposable
{
    private static readonly string Datos = Path.Combine(AppContext.BaseDirectory, "Fixtures", "datos");
    private readonly List<string> _temps = [];

    public void Dispose()
    {
        foreach (var temp in _temps)
            if (Directory.Exists(temp)) Directory.Delete(temp, recursive: true);
    }

    private string NewTemp()
    {
        var path = Path.Combine(Path.GetTempPath(), "srd-import-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        _temps.Add(path);
        return path;
    }

    private string Import(string? dataDir = null)
    {
        var output = NewTemp();
        Program.Import(dataDir ?? Datos, output).Should().NotBeNull();
        return output;
    }

    private static GlossaryEntriesFile ReadPack(string folder, string pack) =>
        JsonSerializer.Deserialize<GlossaryEntriesFile>(File.ReadAllText(Path.Combine(folder, pack + ".json")), GlossaryJson.Options)!;

    private static GlossaryEntry Entry(string folder, string pack, string id) =>
        ReadPack(folder, pack).Entries.Single(e => e.Key == $"srd:{pack}/{id}");

    private string DataDirWith(string synthetic)
    {
        var dir = NewTemp();
        foreach (var file in Directory.GetFiles(Datos, "*.json")) File.Copy(file, Path.Combine(dir, Path.GetFileName(file)));
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", "sinteticos", synthetic), Path.Combine(dir, "criaturas.json"), overwrite: true);
        return dir;
    }

    [Fact]
    public void Creature_maps_core_numbers()
    {
        var stats = Entry(Import(), "criaturas", "aboleth").Creature!;

        (stats.Ac, stats.Hp, stats.InitiativeBonus).Should().Be((17, 150, 7));
    }

    [Fact]
    public void Creature_speed_in_feet_comes_from_the_feet_text()
    {
        var real = Entry(Import(), "criaturas", "aboleth").Creature!.SpeedFeet;
        var computed = Entry(Import(DataDirWith("criatura_sin_pies.json")), "criaturas", "synthetic-no-feet").Creature!.SpeedFeet;

        (real, computed).Should().Be((10, 30));
    }

    [Fact]
    public void Creature_fields_keep_source_order_and_spanish_labels()
    {
        var fields = Entry(Import(), "criaturas", "aboleth").Creature!.Fields;

        fields.Select(f => f.Label).Should().Equal("CA", "Iniciativa", "PG", "Velocidad", "Habilidades", "Sentidos", "Idiomas", "VD");
    }

    [Fact]
    public void Spell_maps_level_school_and_classes()
    {
        var spell = Entry(Import(), "conjuros", "fireball").Spell!;

        (spell.Level, spell.SchoolKey, string.Join(",", spell.ClassKeys)).Should().Be((3, "Evocation", "Sorcerer,Wizard"));
    }

    [Fact]
    public void Embedded_stat_block_becomes_a_box()
    {
        var box = Entry(Import(), "conjuros", "animate-objects").Blocks.Single(b => b.Kind == BlockKind.Box);

        (box.Title, box.Children!.Select(c => c.Kind).First(), box.Children!.Count(c => c.Kind == BlockKind.Table))
            .Should().Be(("Objeto animado", BlockKind.Paragraph, 1));
    }

    [Fact]
    public void Embedded_stat_block_lists_fields_then_ability_table_then_sections()
    {
        var children = Entry(Import(), "conjuros", "animate-objects").Blocks.Single(b => b.Kind == BlockKind.Box).Children!;

        children.Select(c => c.Kind).Should().Equal(
            BlockKind.Paragraph, BlockKind.Fields, BlockKind.Table, BlockKind.Heading, BlockKind.Paragraph);
    }

    [Theory]
    [InlineData("a <b>x</b> b", "a |x*| b")]
    [InlineData("a <strong>x</strong>", "a |x*")]
    [InlineData("<i>x</i> <b><i>y</i></b>", "x~| |y*~")]
    [InlineData("<b>x</b><b>y</b>", "xy*")]
    public void Bold_strong_and_italic_become_span_styles(string html, string expected)
    {
        var spans = SpanParser.Parse(html, "test");

        string.Join("|", spans.Select(Describe)).Should().Be(expected);
    }

    private static string Describe(GlossarySpan s) =>
        s.Text + (s.Style.HasFlag(SpanStyle.Bold) ? "*" : "") + (s.Style.HasFlag(SpanStyle.Italic) ? "~" : "");

    [Fact]
    public void Entities_are_decoded()
        => SpanParser.Parse("Tom &amp; Jerry", "test").Single().Text.Should().Be("Tom & Jerry");

    [Fact]
    public void Unknown_tag_fails_loudly()
    {
        var act = () => Program.Import(DataDirWith("criatura_con_span.json"), NewTemp());

        act.Should().Throw<FormatException>().WithMessage("*synthetic-span*");
    }

    [Fact]
    public void Only_spanish_text_and_english_name_are_written()
    {
        var folder = Import();
        var json = File.ReadAllText(Path.Combine(folder, "estados.json"));
        var cleared = json.Replace("\"nameEn\":\"Blinded\"", "");

        (cleared.Contains("Blinded"), cleared.Contains("Blind\\u"), cleared.Contains("Aveuglé"), cleared.Contains("Blind ")).Should().Be((false, false, false, false));
    }

    [Fact]
    public void Output_is_byte_stable()
    {
        var first = Import();
        var second = Import();

        var same = Directory.GetFiles(first).OrderBy(f => f, StringComparer.Ordinal).Select(f => File.ReadAllBytes(f))
            .Zip(Directory.GetFiles(second).OrderBy(f => f, StringComparer.Ordinal).Select(f => File.ReadAllBytes(f)), (a, b) => a.SequenceEqual(b));

        same.Should().AllBeEquivalentTo(true).And.HaveCount(8);
    }

    [Fact]
    public void Output_is_utf8_without_bom_and_ends_with_a_newline()
    {
        var bytes = File.ReadAllBytes(Path.Combine(Import(), "estados.json"));

        (bytes[0] == 0xEF, bytes[^1]).Should().Be((false, (byte)'\n'));
    }

    [Fact]
    public void Entries_are_sorted_by_key_in_ordinal_order()
    {
        var keys = ReadPack(Import(), "criaturas").Entries.Select(e => e.Key).ToList();

        keys.Should().BeInAscendingOrder(StringComparer.Ordinal);
    }

    [Fact]
    public void Index_summary_and_facets()
    {
        var index = JsonSerializer.Deserialize<GlossaryIndexFile>(File.ReadAllText(Path.Combine(Import(), "indice.json")), GlossaryJson.Options)!;
        var goblin = index.Listings.Single(l => l.Key == "srd:criaturas/goblin-warrior");

        (goblin.Summary, goblin.Facets.Cr, index.Listings.Count).Should().Be(("VD 1/4 · Feérico Pequeño (trasgo), caótico neutral", "1/4", 10));
    }

    [Fact]
    public void Index_summary_follows_the_category()
    {
        var index = JsonSerializer.Deserialize<GlossaryIndexFile>(File.ReadAllText(Path.Combine(Import(), "indice.json")), GlossaryJson.Options)!;
        string Summary(string key) => index.Listings.Single(l => l.Key == key).Summary;

        new[] { Summary("srd:estados/blinded"), Summary("srd:reglas/coins"), Summary("srd:personajes/alert"), Summary("srd:equipo/alchemists-supplies") }
            .Should().Equal("Estado", "Regla", "Dote", "Herramientas de artesano");
    }

    [Fact]
    public void Header_carries_the_pinned_commit()
    {
        var header = ReadPack(Import(), "estados").Header;

        header.Should().Be(new GlossaryPackHeader("compendio-srd", "2d61b5dcabe91ab69d5c4a5a41822eb83c2038cc", "5.2.1", "es"));
        Program.SourceCommit.Should().Be("2d61b5dcabe91ab69d5c4a5a41822eb83c2038cc");
    }

    [Fact]
    public void Missing_arguments_exit_with_code_1()
        => Program.Main([]).Should().Be(1);
}
