using DmCompanion.Screens.Tests.Support;
using Bunit;
using DmCompanion.Core.Vault.Storage;
using DmCompanion.Infrastructure;
using DmCompanion.Screens.Components;
using DmCompanion.Screens.Platform;
using DmCompanion.Screens.Services;
using DmCompanion.UI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DmCompanion.Screens.Tests;

public sealed class CharacterSheetTests : BunitContext, IAsyncLifetime
{
    private readonly TempVault _vault = TempVault.Empty();

    public CharacterSheetTests()
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
        Services.AddGlossaryForTests();
        Services.AddSoundForTests();
        Services.AddScoped<WikilinkInterop>();
        Services.AddSingleton(TimeProvider.System);
        Services.AddSingleton<MapSession>();
        Services.AddSingleton<TokenHitPoints>();
        Services.AddGlossaryForTests();
        Services.AddSingleton<CombatController>();
    }

    Task IAsyncLifetime.InitializeAsync() => Task.CompletedTask;

    async Task IAsyncLifetime.DisposeAsync()
    {
        await Services.GetRequiredService<VaultHost>().DisposeAsync();
        await base.DisposeAsync();
        _vault.Dispose();
    }

    private async Task<IRenderedComponent<CharacterSheet>> RenderSheetAsync(string? campaignSystem, string pcStats, string extraFrontmatter = "")
    {
        if (campaignSystem is not null)
            await File.WriteAllTextAsync(_vault.PathOf("Campana.md"), $"---\ntype: campaign\nname: Campaña\nsystem: {campaignSystem}\n---\n");
        Directory.CreateDirectory(_vault.PathOf("Personajes"));
        await File.WriteAllTextAsync(_vault.PathOf("Personajes/Lyra.md"), $"---\ntype: pc\nname: Lyra\n{extraFrontmatter}{pcStats}---\nTexto.\n");
        var host = Services.GetRequiredService<VaultHost>();
        await host.OpenAsync(_vault.Root);
        return Render<CharacterSheet>(p => p.Add(c => c.Entity, host.Current!.Index.Entities.First(e => e.Title == "Lyra")));
    }

    private const string LyraStats =
        "stats:\n  ac: 14\n  hp: 27\n  speed: 30\n  initiative_bonus: 3\n  abilities: { str: 8, dex: 16, con: 12, int: 13, wis: 14, cha: 10 }\n";

    private static string Stat(IRenderedComponent<CharacterSheet> cut, string label) =>
        cut.FindAll(".ds-stat").First(s => s.QuerySelector(".ds-stat__label")!.TextContent == label)
            .QuerySelector(".ds-stat__value")!.TextContent;

    [Fact]
    public async Task Sheet_shows_the_player_name_when_present() =>
        (await RenderSheetAsync(null, LyraStats, "player: Ana\n")).Find("[data-testid=pc-player]").TextContent.Should().Be("Jugador/a: Ana");

    [Fact]
    public async Task Sheet_shows_the_alias_when_present() =>
        (await RenderSheetAsync(null, LyraStats, "alias: Cuervo\n")).Find("[data-testid=pc-alias]").TextContent.Should().Be("Alias: Cuervo");

    [Fact]
    public async Task Sheet_omits_alias_and_player_when_absent() =>
        (await RenderSheetAsync(null, LyraStats)).FindAll("[data-testid=pc-alias],[data-testid=pc-player]").Should().BeEmpty();

    [Fact]
    public async Task Dnd5e_sheet_shows_armor_class_and_hit_points()
    {
        var cut = await RenderSheetAsync(null, LyraStats);
        (Stat(cut, "AC"), Stat(cut, "HP")).Should().Be(("14", "27"));
    }

    [Fact]
    public async Task Dnd5e_sheet_shows_initiative_with_sign() =>
        Stat(await RenderSheetAsync(null, LyraStats), "Iniciativa").Should().Be("+3");

    [Fact]
    public async Task Dnd5e_sheet_shows_strength_with_typographic_minus() =>
        Stat(await RenderSheetAsync(null, LyraStats), "FUE").Should().Be("8 (−1)");

    [Fact]
    public async Task Dnd5e_sheet_shows_group_headings_except_basic_and_abilities()
    {
        var cut = await RenderSheetAsync(null, LyraStats);
        cut.FindAll("h4").Select(h => h.TextContent).Should().Equal("Salvaciones", "Habilidades");
    }

    [Fact]
    public async Task Alphabetical_group_starts_with_acrobatics_and_its_ability()
    {
        var cut = await RenderSheetAsync(null, LyraStats);
        cut.FindAll(".ds-stat__label").Select(l => l.TextContent).Should().Contain("Acrobacias · DES");
    }

    [Fact]
    public async Task First_skill_is_acrobatics()
    {
        var cut = await RenderSheetAsync(null, LyraStats);
        cut.FindAll(".ds-stats").Last().QuerySelector(".ds-stat__label")!.TextContent.Should().Be("Acrobacias · DES");
    }

    [Fact]
    public async Task Missing_value_with_reason_carries_it_as_title()
    {
        var cut = await RenderSheetAsync(null, "stats:\n  ac: 14\n");
        cut.Find("[title='Falta la característica.']").Should().NotBeNull();
    }

    [Fact]
    public async Task Unknown_system_shows_warning_notice()
    {
        var cut = await RenderSheetAsync("pathfinder", LyraStats);
        cut.Find(".character-sheet .ds-notice").TextContent.Should().Contain("pathfinder");
    }

    [Fact]
    public async Task Unknown_system_shows_key_value_view()
    {
        var cut = await RenderSheetAsync("pathfinder", LyraStats);
        Stat(cut, "stats.ac").Should().Be("14");
    }

    [Fact]
    public async Task Sheet_with_one_warning_shows_singular_mark()
    {
        var cut = await RenderSheetAsync(null, "stats:\n  skills:\n    stealth: pro\n");
        cut.Find(".character-sheet details summary").TextContent.Should().Be("Esta ficha tiene 1 aviso");
    }

    [Fact]
    public async Task Sheet_warning_text_is_listed_inside_details()
    {
        var cut = await RenderSheetAsync(null, "stats:\n  skills:\n    stealth: pro\n");
        cut.Find(".character-sheet details li").TextContent.Should().Contain("stats.skills.stealth");
    }

    [Fact]
    public async Task Sheet_warnings_are_wrapped_in_warning_notice()
    {
        var cut = await RenderSheetAsync(null, "stats:\n  skills:\n    stealth: pro\n");
        cut.Find(".character-sheet details .ds-notice").Should().NotBeNull();
    }

    [Fact]
    public async Task Sheet_with_two_warnings_shows_plural_mark()
    {
        var cut = await RenderSheetAsync(null, "stats:\n  skills:\n    stealth: pro\n    arcana: pro\n");
        cut.Find(".character-sheet details summary").TextContent.Should().Be("Esta ficha tiene 2 avisos");
    }

    [Fact]
    public async Task Sheet_without_warnings_shows_no_mark()
    {
        var cut = await RenderSheetAsync(null, LyraStats);
        cut.FindAll(".character-sheet details").Should().BeEmpty();
    }

    [Fact]
    public async Task Without_the_journal_services_the_sheet_has_no_tabs()
    {
        var cut = await RenderSheetAsync(null, LyraStats);

        cut.FindAll("[role=tab]").Should().BeEmpty();
    }
}
