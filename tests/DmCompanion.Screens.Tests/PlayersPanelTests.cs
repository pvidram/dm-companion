using Bunit;
using DmCompanion.Core.Vault.Storage;
using DmCompanion.Infrastructure;
using DmCompanion.Screens.Components;
using DmCompanion.Screens.Platform;
using DmCompanion.Screens.Services;
using DmCompanion.UI;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DmCompanion.Screens.Tests;

public sealed class PlayersPanelTests : BunitContext, IAsyncLifetime
{
    private const string Lyra = "Personajes/Lyra.md";
    private const string Portrait = "_assets/lyra.png";

    private readonly TempVault _vault = TempVault.FromDemo();

    public PlayersPanelTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddDmCompanionUI();
        Services.AddSingleton<IAppPreferences>(new InMemoryAppPreferences());
        Services.AddSingleton<IVaultServiceFactory>(new FileSystemVaultServiceFactory(NullLoggerFactory.Instance));
        Services.AddSingleton<VaultHost>();
        Services.AddSingleton<PortraitUrlProvider>();
        Services.AddScoped<ShellState>();
        Services.AddScoped<WikilinkInterop>();
    }

    Task IAsyncLifetime.InitializeAsync() => Services.GetRequiredService<VaultHost>().OpenAsync(_vault.Root);

    async Task IAsyncLifetime.DisposeAsync()
    {
        await Services.GetRequiredService<VaultHost>().DisposeAsync();
        await base.DisposeAsync();
        _vault.Dispose();
    }

    private ShellState Shell => Services.GetRequiredService<ShellState>();

    private IRenderedComponent<PlayersPanel> RenderPanel(string? selected = null)
    {
        Shell.Toggle(OverlayPanel.Players);
        if (selected is not null) Shell.SelectPc(selected);
        return Render<PlayersPanel>();
    }

    private static string Stat(IRenderedComponent<PlayersPanel> cut, string label) =>
        cut.FindAll(".ds-stat").First(s => s.QuerySelector(".ds-stat__label")!.TextContent == label)
            .QuerySelector(".ds-stat__value")!.TextContent;

    [Fact]
    public void Lists_both_player_characters() =>
        RenderPanel().FindAll("button.ds-list-row").Select(b => b.TextContent.Trim().Length > 0 ? b.TextContent : "")
            .Should().SatisfyRespectively(
                first => first.Should().Contain("Lyra Vientoalto"),
                second => second.Should().Contain("Torvald Martillo de Sal"));

    [Fact]
    public void Selecting_a_row_selects_that_pc()
    {
        var cut = RenderPanel();
        cut.FindAll("button.ds-list-row").First(b => b.TextContent.Contains("Lyra")).Click();
        cut.WaitForAssertion(() => Shell.SelectedPcPath.Should().Be(Lyra), TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void Lyra_sheet_shows_armor_class_and_hit_points()
    {
        var cut = RenderPanel(Lyra);
        (Stat(cut, "CA"), Stat(cut, "PG")).Should().Be(("14", "27"));
    }

    [Fact]
    public void Lyra_sheet_shows_initiative_with_sign() =>
        Stat(RenderPanel(Lyra), "Iniciativa").Should().Be("+3");

    [Fact]
    public void Lyra_sheet_shows_strength_with_typographic_minus() =>
        Stat(RenderPanel(Lyra), "FUE").Should().Be("8 (−1)");

    [Fact]
    public void Lyra_sheet_shows_dexterity_with_plus() =>
        Stat(RenderPanel(Lyra), "DES").Should().Be("16 (+3)");

    [Fact]
    public void Sheet_renders_the_body_with_wikilinks() =>
        RenderPanel(Lyra).Find("a.wikilink").TextContent.Should().Be("Faro Viejo");

    [Fact]
    public async Task Clicking_a_wikilink_in_the_sheet_opens_that_note()
    {
        var cut = RenderPanel(Lyra);
        await cut.InvokeAsync(() => cut.FindComponent<NoteBody>().Instance.OnWikilinkClicked("Varek", false));
        Shell.SelectedNotePath.Should().Be("Personajes/Varek.md");
    }

    [Fact]
    public void Open_note_action_opens_the_note_in_the_notes_panel()
    {
        var cut = RenderPanel(Lyra);
        cut.Find("button:contains('Abrir nota')").Click();
        cut.WaitForAssertion(() => (Shell.ActivePanel, Shell.SelectedNotePath).Should().Be((OverlayPanel.Notes, Lyra)), TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void Edit_action_starts_editing_the_note()
    {
        var cut = RenderPanel(Lyra);
        cut.Find("button:contains('Editar')").Click();
        cut.WaitForAssertion(() => Shell.EditingPath.Should().Be(Lyra), TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void Missing_portrait_shows_the_initial_without_notice()
    {
        var cut = RenderPanel(Lyra);
        (cut.FindAll(".ds-token__portrait").Count, cut.FindAll(".ds-notice").Count).Should().Be((0, 0));
    }

    [Fact]
    public void Small_portrait_is_shown_as_an_image()
    {
        Directory.CreateDirectory(_vault.PathOf("_assets"));
        File.WriteAllBytes(_vault.PathOf(Portrait), [0x89, 0x50, 0x4E, 0x47]);
        var cut = RenderPanel(Lyra);
        cut.WaitForAssertion(() => cut.FindAll(".character-sheet img.ds-token__portrait").Should().HaveCount(1));
    }

    [Fact]
    public void Too_large_portrait_shows_the_initial_and_a_notice_in_the_sheet()
    {
        Directory.CreateDirectory(_vault.PathOf("_assets"));
        File.WriteAllBytes(_vault.PathOf(Portrait), new byte[512 * 1024 + 1]);
        var cut = RenderPanel(Lyra);
        cut.WaitForAssertion(() =>
            (cut.FindAll(".ds-token__portrait").Count, cut.Find(".character-sheet .ds-token__initial").TextContent,
                cut.FindAll(".character-sheet .ds-notice").Count).Should().Be((0, "L", 1)));
    }

    [Fact]
    public async Task Without_player_characters_shows_an_empty_state()
    {
        File.Delete(_vault.PathOf("Personajes/Lyra.md"));
        File.Delete(_vault.PathOf("Personajes/Torvald.md"));
        await Services.GetRequiredService<VaultHost>().OpenAsync(_vault.Root);
        RenderPanel().FindAll(".ds-empty").Should().HaveCount(1);
    }
}
