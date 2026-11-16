using DmCompanion.Core.Glossary;
using Microsoft.Extensions.Logging;

namespace DmCompanion.Core.Tests.Glossary;

public sealed class GlossaryPackLoaderTests : IDisposable
{
    private readonly List<string> _temps = [];

    public void Dispose()
    {
        foreach (var t in _temps) Directory.Delete(t, recursive: true);
    }

    private string Temp()
    {
        var t = FixturePackSource.CopyToTemp();
        _temps.Add(t);
        return t;
    }

    private static GlossaryPackLoader Loader(IGlossaryPackSource? source = null, ILogger<GlossaryPackLoader>? logger = null)
        => new(source ?? new FixturePackSource(), logger ?? new ListLogger<GlossaryPackLoader>());

    [Fact]
    public void Listings_reads_every_row_of_the_index()
        => Loader().Listings().Value.Should().HaveCount(7);

    [Fact]
    public void Entries_of_a_category_are_keyed_by_key()
        => Loader().Entries(GlossaryCategory.Creature).Value.Should().ContainKey("srd:criaturas/aboleth");

    [Fact]
    public void Get_returns_the_creature_with_its_stats()
        => Loader().Get("srd:criaturas/aboleth")!.Creature!.Ac.Should().Be(17);

    [Fact]
    public void Get_of_an_unknown_key_is_null()
        => Loader().Get("srd:criaturas/nope").Should().BeNull();

    [Fact]
    public void Get_of_a_vault_key_is_null()
        => Loader().Get("vault:notas/x.md").Should().BeNull();

    [Fact]
    public void A_missing_pack_is_an_error_result_not_an_exception()
    {
        var folder = Temp();
        File.Delete(Path.Combine(folder, "conjuros.json"));
        Loader(new FixturePackSource(folder)).Entries(GlossaryCategory.Spell).Error
            .Should().Be("No se encontró el paquete «conjuros».");
    }

    [Fact]
    public void A_corrupt_pack_is_an_error_result_and_logs_only_the_type()
    {
        var folder = Temp();
        File.WriteAllText(Path.Combine(folder, "conjuros.json"), "{ SECRETO-DEL-SRD not json");
        var logger = new ListLogger<GlossaryPackLoader>();
        var result = Loader(new FixturePackSource(folder), logger).Entries(GlossaryCategory.Spell);

        result.Error.Should().Be("El paquete «conjuros» no se pudo leer.");
        logger.Messages.Should().ContainSingle().Which.Should().Contain("JsonException").And.NotContain("SECRETO");
    }

    [Fact]
    public void A_valid_json_without_entries_or_listings_is_an_error_result()
    {
        var folder = Temp();
        File.WriteAllText(Path.Combine(folder, "conjuros.json"), "{}");
        File.WriteAllText(Path.Combine(folder, "indice.json"), "{}");
        var loader = Loader(new FixturePackSource(folder));

        (loader.Entries(GlossaryCategory.Spell).Error, loader.Listings().Error)
            .Should().Be(("El paquete «conjuros» no se pudo leer.", "El paquete «indice» no se pudo leer."));
    }

    [Fact]
    public void Errors_only_reports_packs_already_attempted_and_does_not_load_the_rest()
    {
        var folder = Temp();
        File.Delete(Path.Combine(folder, "conjuros.json"));
        File.Delete(Path.Combine(folder, "objetos.json"));
        var source = new CountingSource(new FixturePackSource(folder));
        var loader = Loader(source);
        var before = (loader.Errors().Count, source.Opens);

        loader.Entries(GlossaryCategory.Spell);

        (before, string.Join("|", loader.Errors()), source.Opens).Should().Be(((0, 0), "No se encontró el paquete «conjuros».", 1));
    }

    [Fact]
    public void A_pack_is_read_only_once()
    {
        var source = new CountingSource(new FixturePackSource());
        var loader = Loader(source);
        loader.Listings();
        loader.Listings();
        source.Opens.Should().Be(1);
    }

    [Fact]
    public void An_error_is_cached_too()
    {
        var folder = Temp();
        File.Delete(Path.Combine(folder, "indice.json"));
        var source = new CountingSource(new FixturePackSource(folder));
        var loader = Loader(source);
        loader.Listings();
        loader.Listings();
        source.Opens.Should().Be(1);
    }

    [Fact]
    public void Spans_keep_angle_brackets_as_text()
        => Loader().Get("srd:reglas/advantage")!.Blocks[0].Text![0].Text.Should().Contain("<script>");

    [Theory]
    [InlineData("srd:conjuros/fireball", GlossaryCategory.Spell)]
    [InlineData("srd:estados/blinded", GlossaryCategory.Condition)]
    public void CategoryOfKey_parses_srd_keys(string key, GlossaryCategory expected)
        => GlossaryPacks.CategoryOfKey(key).Should().Be(expected);

    [Theory]
    [InlineData("vault:x.md")]
    [InlineData("srd:nada/x")]
    [InlineData("srd:conjuros")]
    public void CategoryOfKey_is_null_for_non_srd_or_unknown_packs(string key)
        => GlossaryPacks.CategoryOfKey(key).Should().BeNull();

    [Fact]
    public void Labels_translate_known_keys_and_pass_through_unknown()
    {
        GlossaryLabels.CreatureType("Fiend").Should().Be("Infernal");
        GlossaryLabels.SpellClass("Wizard").Should().Be("Mago");
        GlossaryLabels.School("Evocation").Should().Be("Evocación");
        GlossaryLabels.Rarity("Very Rare").Should().Be("Muy raro");
        GlossaryLabels.Rarity("Mythic").Should().Be("Mythic");
    }

    [Fact]
    public void CategoryLabel_gives_the_chip_texts()
        => GlossaryLabels.CategoryLabel(GlossaryCategory.MagicItem).Should().Be("Objetos mágicos");

    [Theory]
    [InlineData("1/8", 0.125)]
    [InlineData("1/4", 0.25)]
    [InlineData("1/2", 0.5)]
    [InlineData("10", 10)]
    public void CrValue_parses_fractions(string cr, double expected)
        => GlossaryLabels.CrValue(cr).Should().Be((decimal)expected);

    private sealed class CountingSource(IGlossaryPackSource inner) : IGlossaryPackSource
    {
        public int Opens { get; private set; }

        public Stream? Open(string packName)
        {
            Opens++;
            return inner.Open(packName);
        }
    }

    private sealed class ListLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Messages.Add(formatter(state, exception));
    }
}
