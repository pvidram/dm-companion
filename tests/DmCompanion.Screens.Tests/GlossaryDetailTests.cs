using Bunit;
using DmCompanion.Core.Glossary;
using DmCompanion.Core.Tv;
using DmCompanion.Core.Vault.Storage;
using DmCompanion.Infrastructure;
using DmCompanion.Screens.Components;
using DmCompanion.Screens.Platform;
using DmCompanion.Screens.Services;
using DmCompanion.Screens.Tests.Support;
using DmCompanion.UI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DmCompanion.Screens.Tests;

public sealed class GlossaryDetailTests : BunitContext, IAsyncLifetime
{
    private const string AbolethKey = "srd:criaturas/aboleth";
    private const string FireballKey = "srd:conjuros/fireball";

    private readonly TempVault _vault = TempVault.FromDemo();

    public GlossaryDetailTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddDmCompanionUI();
        Services.AddSingleton<IAppPreferences>(new InMemoryAppPreferences());
        Services.AddSingleton<IVaultServiceFactory>(new FileSystemVaultServiceFactory(NullLoggerFactory.Instance));
        Services.AddSingleton<VaultHost>();
        Services.AddSingleton<VaultFileUrls>();
        Services.AddSingleton<NoteImageResolver>();
        Services.AddSingleton<PortraitUrlProvider>();
        Services.AddScoped<ShellState>();
        Services.AddSoundForTests();
        Services.AddGlossaryForTests();
        Services.AddSingleton(TimeProvider.System);
        Services.AddSingleton<MapSession>();
        Services.AddSingleton<TokenHitPoints>();
        Services.AddSingleton<CombatController>();
        Services.AddSingleton<MapLibrary>();
        Services.AddScoped<MapCanvasInterop>();
        Services.AddScoped<TurnStripInterop>();
        Services.AddScoped<DragPanelInterop>();
        Services.AddScoped<WikilinkInterop>();
        Services.AddVaultBrowsingForTests();
        Services.AddSingleton<ITvServer, FakeTvServer>();
        Services.AddSingleton<TvMapFeed>();
        Services.AddSingleton<TvContentFeed>();
        Services.AddSingleton<TvSession>();
    }

    Task IAsyncLifetime.InitializeAsync() => Services.GetRequiredService<VaultHost>().OpenAsync(_vault.Root);

    async Task IAsyncLifetime.DisposeAsync()
    {
        await Services.GetRequiredService<VaultHost>().DisposeAsync();
        await base.DisposeAsync();
        _vault.Dispose();
    }

    private ShellState Shell => Services.GetRequiredService<ShellState>();

    private async Task<IRenderedComponent<DmShell>> OpenAsync(string key)
    {
        var cut = Render<DmShell>();
        await cut.InvokeAsync(() => Shell.OpenGlossary(key));
        return cut;
    }

    private static AngleSharp.Dom.IElement Panel(IRenderedComponent<DmShell> cut) => cut.Find("section.glossary-panel");

    private static AngleSharp.Dom.IElement? Button(IRenderedComponent<DmShell> cut, string label) =>
        Panel(cut).QuerySelectorAll("button").FirstOrDefault(b => b.TextContent.Trim() == label);

    private async Task WriteNoteAsync(string relativePath, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_vault.PathOf(relativePath))!);
        await File.WriteAllTextAsync(_vault.PathOf(relativePath), content);
        await Services.GetRequiredService<VaultHost>().OpenAsync(_vault.Root);
    }

    [Fact]
    public async Task Creature_detail_shows_ac_hp_and_ability_table()
    {
        var cut = await OpenAsync(AbolethKey);

        var text = Panel(cut).TextContent;
        text.Should().Contain("17").And.Contain("150");
        Panel(cut).QuerySelectorAll(".ds-glossary-abilities tbody tr").Should().HaveCount(6);
    }

    [Fact]
    public async Task Spell_detail_shows_header_and_range()
    {
        var cut = await OpenAsync(FireballKey);

        var text = Panel(cut).TextContent;
        text.Should().Contain("Alcance").And.Contain("45 m");
        Panel(cut).QuerySelector("em")!.TextContent.Should().Contain("3");
    }

    [Fact]
    public async Task Rule_text_is_rendered_encoded()
    {
        var rule = Services.GetRequiredService<GlossaryService>().Index
            .Search(new GlossaryQuery(null, GlossaryCategory.Rule, null)).First();

        var cut = await OpenAsync(rule.Key);

        Panel(cut).QuerySelectorAll("script").Should().BeEmpty();
    }

    [Fact]
    public async Task Copy_to_vault_creates_a_monster_note_and_opens_it()
    {
        var cut = await OpenAsync(AbolethKey);

        await cut.InvokeAsync(() => Button(cut, "Copiar al vault")!.Click());

        cut.WaitForAssertion(() => Shell.SelectedGlossaryKey.Should().Be("vault:Glosario/Aboleth.md"));
        File.Exists(_vault.PathOf("Glosario/Aboleth.md")).Should().BeTrue();
        Services.GetRequiredService<GlossaryService>().Index.Get("vault:Glosario/Aboleth.md").Should().NotBeNull();
    }

    [Fact]
    public async Task Copying_a_spell_marks_it_as_yours_and_hides_copy()
    {
        var cut = await OpenAsync(FireballKey);
        await cut.InvokeAsync(() => Button(cut, "Copiar al vault")!.Click());
        cut.WaitForAssertion(() => Shell.SelectedGlossaryKey.Should().StartWith("vault:"));
        var glossary = Services.GetRequiredService<GlossaryService>();
        cut.WaitForAssertion(() => glossary.Index.OverriddenBy(FireballKey).Should().NotBeNull());

        await cut.InvokeAsync(() => Shell.OpenGlossary(FireballKey));

        // La búsqueda también devuelve «Bola de fuego de explosión retardada»: solo cuenta la fila de nombre exacto.
        var rows = glossary.Index.Search(new GlossaryQuery(Text: "bola de fuego")).Where(r => r.Name == "Bola de fuego").ToList();
        (rows.Count, rows[0].IsVault, rows[0].Summary, Button(cut, "Copiar al vault")).Should().Be((1, true, "Tu nota", null));
    }

    [Fact]
    public async Task Copy_to_vault_never_overwrites()
    {
        await WriteNoteAsync("Glosario/Aboleth.md", "---\ntype: rule\nname: Otro\n---\nMío.\n");
        var cut = await OpenAsync(AbolethKey);
        // La nota «Otro» no tapa al SRD: el botón sigue disponible y choca con el fichero existente.
        await cut.InvokeAsync(() => Button(cut, "Copiar al vault")!.Click());

        cut.WaitForAssertion(() => Panel(cut).TextContent.Should().Contain("Ya existe «Aboleth» en el vault."));
        File.ReadAllText(_vault.PathOf("Glosario/Aboleth.md")).Should().Be("---\ntype: rule\nname: Otro\n---\nMío.\n");
        Button(cut, "Abrir").Should().NotBeNull();
    }

    [Fact]
    public async Task Vault_entry_shows_note_reader_and_see_original()
    {
        await WriteNoteAsync("Glosario/Aboleth.md", "---\ntype: monster\nname: Aboleth\n---\nMi versión.\n");

        var cut = await OpenAsync("vault:Glosario/Aboleth.md");

        Panel(cut).QuerySelector("article.note-reader")!.TextContent.Should().Contain("Mi versión.");
        Button(cut, "Ver original del SRD").Should().NotBeNull();
    }

    [Fact]
    public async Task See_original_opens_the_srd_entry()
    {
        await WriteNoteAsync("Glosario/Aboleth.md", "---\ntype: monster\nname: Aboleth\n---\nMi versión.\n");
        var cut = await OpenAsync("vault:Glosario/Aboleth.md");

        await cut.InvokeAsync(() => Button(cut, "Ver original del SRD")!.Click());

        Shell.SelectedGlossaryKey.Should().Be(AbolethKey);
    }

    [Fact]
    public async Task Overridden_srd_entry_points_to_your_version()
    {
        await WriteNoteAsync("Glosario/Aboleth.md", "---\ntype: monster\nname: Aboleth\n---\nMi versión.\n");
        var cut = await OpenAsync(AbolethKey);

        Panel(cut).TextContent.Should().Contain("Tienes tu propia versión");
        Button(cut, "Copiar al vault").Should().BeNull();
        await cut.InvokeAsync(() => Button(cut, "Abrir mi versión")!.Click());
        Shell.SelectedGlossaryKey.Should().Be("vault:Glosario/Aboleth.md");
    }

    [Fact]
    public async Task Srd_detail_has_exactly_one_actions_bar()
    {
        var cut = await OpenAsync(AbolethKey);

        Panel(cut).QuerySelectorAll(".glossary-detail__actions").Should().HaveCount(1);
    }

    [Fact]
    public async Task Vault_detail_has_exactly_one_actions_bar()
    {
        await WriteNoteAsync("Glosario/Aboleth.md", "---\ntype: monster\nname: Aboleth\n---\nMi versión.\n");

        var cut = await OpenAsync("vault:Glosario/Aboleth.md");

        Panel(cut).QuerySelectorAll(".glossary-detail__actions").Should().HaveCount(1);
    }

    [Fact]
    public async Task Actions_bar_is_the_first_child_of_the_detail()
    {
        var cut = await OpenAsync(AbolethKey);

        Panel(cut).QuerySelector(".glossary-detail")!.FirstElementChild!.ClassList.Should().Contain("glossary-detail__actions");
    }

    [Fact]
    public async Task Creature_detail_offers_add_to_map_with_an_editable_map_open()
    {
        await Services.GetRequiredService<MapSession>().OpenAsync("Mapas/Boceto de la cueva.md");

        var cut = await OpenAsync(AbolethKey);

        Button(cut, "Añadir al mapa").Should().NotBeNull();
    }

    [Fact]
    public async Task Spell_detail_offers_show_on_tv_when_the_tv_is_on()
    {
        await Services.GetRequiredService<TvSession>().EnableAsync();

        var cut = await OpenAsync(FireballKey);

        Button(cut, "Mostrar en TV").Should().NotBeNull();
    }

    [Fact]
    public async Task Creature_detail_never_offers_show_on_tv()
    {
        await Services.GetRequiredService<TvSession>().EnableAsync();

        var cut = await OpenAsync(AbolethKey);

        Button(cut, "Mostrar en TV").Should().BeNull();
    }

    [Fact]
    public async Task Without_the_journal_services_a_creature_sheet_has_no_players_section()
    {
        var cut = await OpenAsync(AbolethKey);

        Panel(cut).QuerySelectorAll(".journal-creature").Should().BeEmpty();
    }
}
