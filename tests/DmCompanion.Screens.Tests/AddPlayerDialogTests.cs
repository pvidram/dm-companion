using DmCompanion.Screens.Tests.Support;
using Bunit;
using DmCompanion.Core.Vault;
using DmCompanion.Core.Vault.Storage;
using DmCompanion.Infrastructure;
using DmCompanion.Screens.Components;
using DmCompanion.Screens.Platform;
using DmCompanion.Screens.Services;
using DmCompanion.UI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DmCompanion.Screens.Tests;

public sealed class AddPlayerDialogTests : BunitContext, IAsyncLifetime
{
    private readonly TempVault _vault = TempVault.FromDemo();

    public AddPlayerDialogTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddDmCompanionUI();
        Services.AddSingleton<IAppPreferences>(new InMemoryAppPreferences());
        Services.AddSingleton<IVaultServiceFactory>(new FileSystemVaultServiceFactory(NullLoggerFactory.Instance));
        Services.AddSingleton<VaultHost>();
        Services.AddScoped<ShellState>();
        Services.AddSoundForTests();
    }

    private ShellState Shell => Services.GetRequiredService<ShellState>();

    Task IAsyncLifetime.InitializeAsync() => Task.CompletedTask;

    async Task IAsyncLifetime.DisposeAsync()
    {
        await Services.GetRequiredService<VaultHost>().DisposeAsync();
        await base.DisposeAsync();
        _vault.Dispose();
    }

    private async Task<IRenderedComponent<AddPlayerDialog>> RenderOpenAsync(string? system = null)
    {
        if (system is not null)
            File.WriteAllText(_vault.PathOf("Campaña.md"), $"---\ntype: campaign\nsystem: {system}\n---\n# Campaña\n");
        await Services.GetRequiredService<VaultHost>().OpenAsync(_vault.Root);
        var cut = Render<AddPlayerDialog>();
        await cut.InvokeAsync(Shell.OpenAddPlayer);
        return cut;
    }

    private static Task TypeAsync(IRenderedComponent<AddPlayerDialog> cut, string testId, string value) =>
        cut.InvokeAsync(() => cut.Find($"[data-testid={testId}]").Input(value));

    private static Task ChangeStepperAsync(IRenderedComponent<AddPlayerDialog> cut, string label, string value) =>
        cut.InvokeAsync(() => cut.Find($"input[aria-label='{label}']").Change(value));

    private static Task ClickCreateAsync(IRenderedComponent<AddPlayerDialog> cut) =>
        cut.InvokeAsync(() => cut.FindAll("footer button").First(b => b.TextContent.Trim() == "Crear").Click());

    private static bool CreateDisabled(IRenderedComponent<AddPlayerDialog> cut) =>
        cut.FindAll("footer button").First(b => b.TextContent.Trim() == "Crear").HasAttribute("disabled");

    [Fact]
    public async Task Create_is_disabled_without_a_name()
    {
        var cut = await RenderOpenAsync();

        CreateDisabled(cut).Should().BeTrue();
    }

    [Fact]
    public async Task Create_is_enabled_with_a_name()
    {
        var cut = await RenderOpenAsync();

        await TypeAsync(cut, "add-player-name", "Mira Cuervo");

        CreateDisabled(cut).Should().BeFalse();
    }

    [Fact]
    public async Task Dnd5e_form_shows_the_ten_fields_with_defaults()
    {
        var cut = await RenderOpenAsync();

        var labels = string.Join(",", cut.FindAll(".ds-stepper .ds-field__label").Select(l => l.TextContent));
        (labels, cut.Find("input[aria-label='CA']").GetAttribute("value"), cut.Find("input[aria-label='Nivel']").GetAttribute("value"))
            .Should().Be(("CA,PG,Velocidad,Nivel,Fuerza,Destreza,Constitución,Inteligencia,Sabiduría,Carisma", "10", "1"));
    }

    [Fact]
    public async Task Creating_writes_the_note_closes_the_dialog_and_selects_the_new_sheet()
    {
        var cut = await RenderOpenAsync();
        await TypeAsync(cut, "add-player-name", "Mira Cuervo");
        await TypeAsync(cut, "add-player-alias", "Cuervo");
        await TypeAsync(cut, "add-player-player", "Ana");

        await ClickCreateAsync(cut);

        cut.WaitForAssertion(() => (File.Exists(_vault.PathOf("Personajes/Mira Cuervo.md")), Shell.AddPlayerOpen, Shell.SelectedPcPath, Shell.PlayerTab)
            .Should().Be((true, false, "Personajes/Mira Cuervo.md", PlayerTab.Sheet)));
    }

    [Fact]
    public async Task A_second_tap_while_creating_is_ignored_and_leaves_no_stale_notice()
    {
        var cut = await RenderOpenAsync();
        await TypeAsync(cut, "add-player-name", "Mira Cuervo");

        await cut.InvokeAsync(() =>
        {
            var create = cut.FindAll("footer button").First(b => b.TextContent.Trim() == "Crear");
            create.Click();
            create.Click();
        });

        cut.WaitForAssertion(() => (File.Exists(_vault.PathOf("Personajes/Mira Cuervo.md")), Shell.AddPlayerOpen, cut.FindAll("[data-testid=add-player-notice]").Count)
            .Should().Be((true, false, 0)));
    }

    [Fact]
    public async Task Out_of_range_value_shows_an_inline_error_and_disables_create()
    {
        var cut = await RenderOpenAsync();
        await TypeAsync(cut, "add-player-name", "Mira Cuervo");

        await ChangeStepperAsync(cut, "Fuerza", "0");

        (cut.Find("[data-testid=add-player-errors]").TextContent, CreateDisabled(cut), File.Exists(_vault.PathOf("Personajes/Mira Cuervo.md")))
            .Should().Match<(string, bool, bool)>(t => t.Item1.Contains("Fuerza") && t.Item2 && !t.Item3);
    }

    [Fact]
    public async Task Non_numeric_stepper_text_keeps_the_previous_value()
    {
        var cut = await RenderOpenAsync();

        await ChangeStepperAsync(cut, "Fuerza", "abc");

        cut.Find("input[aria-label='Fuerza']").GetAttribute("value").Should().Be("10");
    }

    [Fact]
    public async Task Existing_name_shows_a_notice_keeps_the_dialog_open_and_the_form_filled()
    {
        var original = File.ReadAllText(_vault.PathOf("Personajes/Torvald.md"));
        var cut = await RenderOpenAsync();
        await TypeAsync(cut, "add-player-name", "Torvald");

        await ClickCreateAsync(cut);

        cut.WaitForAssertion(() => (
                cut.Find("[data-testid=add-player-notice]").TextContent.Contains("Ya existe"),
                Shell.AddPlayerOpen,
                cut.Find("[data-testid=add-player-name]").GetAttribute("value"),
                File.ReadAllText(_vault.PathOf("Personajes/Torvald.md")) == original)
            .Should().Be((true, true, "Torvald", true)));
    }

    [Fact]
    public async Task Cosmere_campaign_shows_attributes_and_resources()
    {
        var cut = await RenderOpenAsync("cosmere");

        var labels = cut.FindAll(".ds-stepper .ds-field__label").Select(l => l.TextContent).ToList();
        labels.Should().Contain(["Fuerza", "Velocidad", "Salud (máx.)"]).And.NotContain("CA");
    }

    [Fact]
    public async Task Unknown_system_shows_only_the_text_fields_and_a_notice()
    {
        var cut = await RenderOpenAsync("otro");

        (cut.FindAll(".ds-stepper").Count, cut.Markup.Contains("Sistema desconocido"))
            .Should().Be((0, true));
    }

    [Fact]
    public async Task Unknown_system_creates_the_note_with_empty_stats()
    {
        var cut = await RenderOpenAsync("otro");
        await TypeAsync(cut, "add-player-name", "Mira Cuervo");

        await ClickCreateAsync(cut);

        cut.WaitForAssertion(() => File.ReadAllText(_vault.PathOf("Personajes/Mira Cuervo.md")).Should().Contain("stats: {}"));
    }

    [Fact]
    public async Task Reopening_resets_the_form()
    {
        var cut = await RenderOpenAsync();
        await TypeAsync(cut, "add-player-name", "Mira");
        await ChangeStepperAsync(cut, "Fuerza", "18");

        await cut.InvokeAsync(Shell.CloseAddPlayer);
        await cut.InvokeAsync(Shell.OpenAddPlayer);

        (cut.Find("[data-testid=add-player-name]").GetAttribute("value"), cut.Find("input[aria-label='Fuerza']").GetAttribute("value"))
            .Should().Be((string.Empty, "10"));
    }
}
