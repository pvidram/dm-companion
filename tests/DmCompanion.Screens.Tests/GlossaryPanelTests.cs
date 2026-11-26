using Bunit;
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

public sealed class GlossaryPanelTests : BunitContext, IAsyncLifetime
{
    private readonly TempVault _vault = TempVault.FromDemo();

    public GlossaryPanelTests()
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
        Services.AddWindowingForTests();
        Services.AddVaultBrowsingForTests();
    }

    Task IAsyncLifetime.InitializeAsync() => Services.GetRequiredService<VaultHost>().OpenAsync(_vault.Root);

    async Task IAsyncLifetime.DisposeAsync()
    {
        await Services.GetRequiredService<VaultHost>().DisposeAsync();
        await base.DisposeAsync();
        _vault.Dispose();
    }

    private ShellState Shell => Services.GetRequiredService<ShellState>();

    private async Task<IRenderedComponent<DmShell>> OpenPanelAsync()
    {
        var cut = Render<DmShell>();
        await cut.InvokeAsync(() => cut.Find("button[aria-label='Glosario']").Click());
        return cut;
    }

    private static AngleSharp.Dom.IElement Panel(IRenderedComponent<DmShell> cut) => cut.Find("section.glossary-panel");

    private static IReadOnlyList<string> Rows(IRenderedComponent<DmShell> cut) =>
        Panel(cut).QuerySelectorAll("button.ds-list-row").Select(b => b.TextContent).ToList();

    private static Task Search(IRenderedComponent<DmShell> cut, string text) => cut.InvokeAsync(() => Panel(cut).QuerySelector("input[type=search]")!.Input(text));

    private static Task Chip(IRenderedComponent<DmShell> cut, string label) =>
        cut.InvokeAsync(() => Panel(cut).QuerySelectorAll("button.glossary-chip").First(b => b.TextContent.Trim() == label).Click());

    [Fact]
    public void Rail_has_a_Glossary_button()
    {
        var cut = Render<DmShell>();

        cut.Find("button[aria-label='Glosario']").GetAttribute("aria-pressed").Should().Be("false");
    }

    [Fact]
    public async Task Opening_the_panel_lists_entries()
    {
        var cut = await OpenPanelAsync();

        Rows(cut).Should().NotBeEmpty();
    }

    [Fact]
    public async Task Searching_fireball_finds_Bola_de_fuego()
    {
        var cut = await OpenPanelAsync();

        await Search(cut, "bola de fuego");

        Rows(cut).Should().Contain(row => row.Contains("Bola de fuego"));
    }

    [Fact]
    public async Task Category_chip_filters()
    {
        var cut = await OpenPanelAsync();

        await Chip(cut, "Conjuros");

        Rows(cut).Should().NotBeEmpty().And.OnlyContain(row => !row.Contains("Aboleth"));
        Panel(cut).QuerySelectorAll("button.glossary-chip").Single(b => b.GetAttribute("aria-pressed") == "true").TextContent.Trim().Should().Be("Conjuros");
    }

    [Fact]
    public async Task Cr_filter_hides_the_aboleth()
    {
        var cut = await OpenPanelAsync();
        await Chip(cut, "Criaturas");
        await Search(cut, "aboleth");
        Rows(cut).Should().Contain(row => row.Contains("Aboleth"));

        var upTo = Panel(cut).QuerySelector("select[aria-label='VD hasta']")!;
        await cut.InvokeAsync(() => upTo.Change(upTo.QuerySelectorAll("option").Single(o => o.TextContent == "2").GetAttribute("value")!));

        Rows(cut).Should().NotContain(row => row.Contains("Aboleth"));
    }

    [Fact]
    public async Task Vault_entries_are_marked_Tuya()
    {
        Directory.CreateDirectory(_vault.PathOf("Glosario"));
        await File.WriteAllTextAsync(_vault.PathOf("Glosario/Investidura.md"), "---\ntype: rule\nname: Investidura\n---\nTexto.\n");
        await Services.GetRequiredService<VaultHost>().OpenAsync(_vault.Root);
        var cut = await OpenPanelAsync();

        await Search(cut, "investidura");

        Panel(cut).QuerySelector("button.ds-list-row .ds-condition")!.TextContent.Trim().Should().Be("Tuya");
    }

    [Fact]
    public async Task Selecting_a_row_opens_its_detail_and_Atras_returns()
    {
        var cut = await OpenPanelAsync();
        await Chip(cut, "Criaturas");
        await Search(cut, "guerrero goblin");

        await cut.InvokeAsync(() => Panel(cut).QuerySelectorAll("button.ds-list-row").First(b => b.TextContent.Contains("Guerrero goblin")).Click());
        Shell.SelectedGlossaryKey.Should().Be("srd:criaturas/goblin-warrior");
        Panel(cut).QuerySelector("h3")!.TextContent.Should().Contain("Guerrero goblin");

        await cut.InvokeAsync(() => Panel(cut).QuerySelectorAll("button").First(b => b.TextContent.Trim() == "Atrás").Click());

        Shell.SelectedGlossaryKey.Should().BeNull();
    }

    [Fact]
    public async Task Attribution_is_shown()
    {
        var cut = await OpenPanelAsync();

        Panel(cut).TextContent.Should().Contain("Creative Commons");
    }
}
