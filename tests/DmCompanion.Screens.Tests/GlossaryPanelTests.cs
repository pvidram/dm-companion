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
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

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
        Services.AddScoped<DragPanelInterop>();
        Services.AddVaultBrowsingForTests();
    }

    async Task IAsyncLifetime.InitializeAsync()
    {
        await Services.GetRequiredService<VaultHost>().OpenAsync(_vault.Root);
        await Services.WaitForAudioLibraryAsync(TimeSpan.FromSeconds(10));
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await Services.GetRequiredService<VaultHost>().DisposeAsync();
        await base.DisposeAsync();
        _vault.Dispose();
    }

    private ShellState Shell => Services.GetRequiredService<ShellState>();

    private IRenderedComponent<DmShell> OpenPanel()
    {
        var cut = Render<DmShell>();
        cut.Find("button[aria-label='Glosario']").Click();
        // El cuerpo del panel se pinta en un re-render que puede no haber terminado al volver el clic: se espera a él.
        cut.WaitForElement("section.glossary-panel .glossary-tabs", Timeout);
        return cut;
    }

    private static AngleSharp.Dom.IElement Panel(IRenderedComponent<DmShell> cut) => cut.Find("section.glossary-panel");

    private static IReadOnlyList<string> Rows(IRenderedComponent<DmShell> cut) =>
        Panel(cut).QuerySelectorAll("button.ds-list-row").Select(b => b.TextContent).ToList();

    private static void Search(IRenderedComponent<DmShell> cut, string text) => Panel(cut).QuerySelector("input[type=search]")!.Input(text);

    private static void Chip(IRenderedComponent<DmShell> cut, string label) =>
        Panel(cut).QuerySelectorAll("button.glossary-chip").First(b => b.TextContent.Trim() == label).Click();

    [Fact]
    public void Rail_has_a_Glossary_button()
    {
        var cut = Render<DmShell>();

        cut.Find("button[aria-label='Glosario']").GetAttribute("aria-pressed").Should().Be("false");
    }

    [Fact]
    public void Opening_the_panel_lists_entries()
    {
        var cut = OpenPanel();

        Rows(cut).Should().NotBeEmpty();
    }

    [Fact]
    public void Searching_fireball_finds_Bola_de_fuego()
    {
        var cut = OpenPanel();

        Search(cut, "bola de fuego");

        Rows(cut).Should().Contain(row => row.Contains("Bola de fuego"));
    }

    [Fact]
    public void Category_chip_filters()
    {
        var cut = OpenPanel();

        Chip(cut, "Conjuros");

        Rows(cut).Should().NotBeEmpty().And.OnlyContain(row => !row.Contains("Aboleth"));
        Panel(cut).QuerySelectorAll("button.glossary-chip").Single(b => b.GetAttribute("aria-pressed") == "true").TextContent.Trim().Should().Be("Conjuros");
    }

    [Fact]
    public void Cr_filter_hides_the_aboleth()
    {
        var cut = OpenPanel();
        Chip(cut, "Criaturas");
        Search(cut, "aboleth");
        Rows(cut).Should().Contain(row => row.Contains("Aboleth"));

        var upTo = Panel(cut).QuerySelector("select[aria-label='VD hasta']")!;
        upTo.Change(upTo.QuerySelectorAll("option").Single(o => o.TextContent == "2").GetAttribute("value")!);

        Rows(cut).Should().NotContain(row => row.Contains("Aboleth"));
    }

    [Fact]
    public async Task Vault_entries_are_marked_Tuya()
    {
        Directory.CreateDirectory(_vault.PathOf("Glosario"));
        await File.WriteAllTextAsync(_vault.PathOf("Glosario/Investidura.md"), "---\ntype: rule\nname: Investidura\n---\nTexto.\n");
        await Services.GetRequiredService<VaultHost>().OpenAsync(_vault.Root);
        var cut = OpenPanel();

        Search(cut, "investidura");

        Panel(cut).QuerySelector("button.ds-list-row .ds-condition")!.TextContent.Trim().Should().Be("Tuya");
    }

    [Fact]
    public void Selecting_a_row_opens_its_detail_and_Atras_returns()
    {
        var cut = OpenPanel();
        Chip(cut, "Criaturas");
        Search(cut, "guerrero goblin");

        Panel(cut).QuerySelectorAll("button.ds-list-row").First(b => b.TextContent.Contains("Guerrero goblin")).Click();
        Shell.SelectedGlossaryKey.Should().Be("srd:criaturas/goblin-warrior");
        Panel(cut).QuerySelector("h3")!.TextContent.Should().Contain("Guerrero goblin");

        Panel(cut).QuerySelectorAll("button").First(b => b.TextContent.Trim() == "Atrás").Click();

        Shell.SelectedGlossaryKey.Should().BeNull();
    }

    [Fact]
    public void Attribution_is_shown()
    {
        var cut = OpenPanel();

        Panel(cut).TextContent.Should().Contain("Creative Commons");
    }
}
