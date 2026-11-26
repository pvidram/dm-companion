using DmCompanion.Screens.Tests.Support;
using Bunit;
using DmCompanion.Core.Maps;
using DmCompanion.Core.Vault.Storage;
using DmCompanion.Infrastructure;
using DmCompanion.Screens.Components;
using DmCompanion.Screens.Platform;
using DmCompanion.Screens.Services;
using DmCompanion.UI;
using DmCompanion.UI.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DmCompanion.Screens.Tests;

public sealed class PlayerPreviewOverlayTests : BunitContext, IAsyncLifetime
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly TempVault _vault = TempVault.Empty();
    private readonly CountingFactory _factory = new();

    public PlayerPreviewOverlayTests()
    {
        Services.AddDmCompanionUI();
        Services.AddSingleton<IAppPreferences>(new InMemoryAppPreferences());
        Services.AddSingleton<IVaultServiceFactory>(_factory);
        Services.AddSingleton<VaultHost>();
        Services.AddSingleton(TimeProvider.System);
        Services.AddSingleton<MapSession>();
        Services.AddSingleton<TokenHitPoints>();
        Services.AddGlossaryForTests();
        Services.AddSingleton<CombatController>();
        Services.AddSingleton<MapLibrary>();
        Services.AddSingleton<VaultFileUrls>();
        Services.AddSingleton<NoteImageResolver>();
        Services.AddSingleton<PortraitUrlProvider>();
        Services.AddScoped<ShellState>();
        Services.AddSoundForTests();
        Services.AddScoped<MapCanvasInterop>();
        Services.AddScoped<TurnStripInterop>();
        Services.AddWindowingForTests();
        Services.AddScoped<PlayerPreview>();
        Services.AddScoped<PlayerPreviewInterop>();

        JSInterop.SetupVoid("mapCanvas.load", _ => true).SetVoidResult();
        JSInterop.SetupVoid("mapCanvas.render", _ => true).SetVoidResult();
        JSInterop.SetupVoid("mapCanvas.setMode", _ => true).SetVoidResult();
        JSInterop.SetupVoid("mapCanvas.setFogTool", _ => true).SetVoidResult();
        JSInterop.SetupVoid("mapCanvas.fogDraft", _ => true).SetVoidResult();
        JSInterop.SetupVoid("mapCanvas.dispose", _ => true).SetVoidResult();
        JSInterop.SetupVoid("playerPreview.show", _ => true).SetVoidResult();
        JSInterop.SetupVoid("playerPreview.update", _ => true).SetVoidResult();
        JSInterop.SetupVoid("playerPreview.hide", _ => true).SetVoidResult();
    }

    Task IAsyncLifetime.InitializeAsync() => Task.CompletedTask;

    async Task IAsyncLifetime.DisposeAsync()
    {
        await Services.GetRequiredService<VaultHost>().DisposeAsync();
        await base.DisposeAsync();
        _vault.Dispose();
    }

    private MapSession Session => Services.GetRequiredService<MapSession>();
    private PlayerPreview Preview => Services.GetRequiredService<PlayerPreview>();

    private int Calls(string identifier) => JSInterop.Invocations.Count(i => i.Identifier == identifier);

    // Las llamadas JS no provocan render: WaitForAssertion no volvería a comprobar, así que se sondea.
    private static async Task EventuallyAsync(Action assertion)
    {
        var deadline = DateTime.UtcNow + Timeout;
        while (true)
        {
            try
            {
                assertion();
                return;
            }
            catch (Exception) when (DateTime.UtcNow < deadline)
            {
                await Task.Delay(20);
            }
        }
    }

    private async Task OpenMapAsync()
    {
        MapTestVault.WriteImageMap(_vault, MapTestVault.StateWithGrid(true));
        await Services.GetRequiredService<VaultHost>().OpenAsync(_vault.Root);
        await Session.OpenAsync(MapTestVault.MapNote);
    }

    private static AngleSharp.Dom.IElement Button(IRenderedComponent<MapCanvas> cut, string label) =>
        cut.Find($"[aria-label='{label}']");

    private async Task<IRenderedComponent<MapCanvas>> RenderMapAsync()
    {
        await OpenMapAsync();
        return Render<MapCanvas>();
    }

    [Fact]
    public async Task The_Mapa_group_has_the_Vista_de_jugadores_button_with_the_Eye_icon_not_pressed()
    {
        var cut = await RenderMapAsync();

        var group = cut.FindAll("[aria-label='Mapa']").Single();
        var button = group.QuerySelector("[aria-label='Vista de jugadores']")!;

        (button.GetAttribute("aria-pressed"), button.InnerHtml.Contains(IconFiles.Url(IconName.Eye)))
            .Should().Be(("false", true));
    }

    [Fact]
    public async Task Activating_shows_the_notice_and_the_button_is_pressed()
    {
        var cut = await RenderMapAsync();

        Button(cut, "Vista de jugadores").Click();

        cut.WaitForAssertion(() => cut.Find(".player-preview__notice").TextContent.Should().Be("Vista de jugadores"), Timeout);
        Button(cut, "Vista de jugadores").GetAttribute("aria-pressed").Should().Be("true");
    }

    [Fact]
    public async Task Activating_calls_show_on_the_JS_side()
    {
        var cut = await RenderMapAsync();

        Button(cut, "Vista de jugadores").Click();

        await EventuallyAsync(() => Calls("playerPreview.show").Should().Be(1));
    }

    [Fact]
    public async Task A_map_mutation_calls_update()
    {
        var cut = await RenderMapAsync();
        Button(cut, "Vista de jugadores").Click();
        await EventuallyAsync(() => Calls("playerPreview.show").Should().Be(1));

        Session.Mutate(e => { e.SetGrid(new GridSettings(true, 80, 0, 0)); return true; });

        await EventuallyAsync(() => Calls("playerPreview.update").Should().BeGreaterThan(0));
    }

    [Fact]
    public async Task While_active_the_other_toolbar_buttons_are_disabled_and_the_view_button_is_not()
    {
        var cut = await RenderMapAsync();

        Button(cut, "Vista de jugadores").Click();

        cut.WaitForAssertion(() =>
        {
            var toolbar = cut.Find("[aria-label='Herramientas del mapa']");
            var enabled = toolbar.QuerySelectorAll("button, input").Where(b => !b.HasAttribute("disabled"))
                .Select(b => b.GetAttribute("aria-label")).ToArray();
            enabled.Should().BeEquivalentTo(["Vista de jugadores", "Ocultar la barra de herramientas"]);
        }, Timeout);
    }

    [Fact]
    public async Task While_active_the_fog_tools_are_disabled()
    {
        var cut = await RenderMapAsync();
        Button(cut, "Niebla").Click();
        Button(cut, "Vista de jugadores").Click();

        cut.WaitForAssertion(() =>
        {
            var fogTools = cut.FindAll("[role='group'][aria-label='Niebla'] button");
            (fogTools.Count, fogTools.Count(b => !b.HasAttribute("disabled"))).Should().Be((5, 0));
        }, Timeout);
    }

    [Fact]
    public async Task An_open_grid_panel_is_not_rendered_while_the_view_is_active()
    {
        var cut = await RenderMapAsync();
        Button(cut, "Ajustes de cuadrícula").Click();
        cut.FindAll("[aria-label='Ajustes de la cuadrícula']").Should().HaveCount(1);

        Button(cut, "Vista de jugadores").Click();

        cut.WaitForAssertion(() => cut.FindAll("[aria-label='Ajustes de la cuadrícula']").Should().BeEmpty(), Timeout);
        var settings = Button(cut, "Ajustes de cuadrícula");
        (settings.GetAttribute("aria-pressed"), settings.GetAttribute("aria-expanded")).Should().Be(("false", "false"));
    }

    [Fact]
    public async Task Deactivating_calls_hide_and_removes_the_overlay()
    {
        var cut = await RenderMapAsync();
        Button(cut, "Vista de jugadores").Click();
        await EventuallyAsync(() => Calls("playerPreview.show").Should().Be(1));

        Button(cut, "Vista de jugadores").Click();

        await EventuallyAsync(() => (Calls("playerPreview.hide"), cut.FindAll(".player-preview").Count).Should().Be((1, 0)));
    }

    [Fact]
    public async Task It_works_without_the_TV_service()
    {
        var cut = await RenderMapAsync();

        Button(cut, "Vista de jugadores").Click();

        await EventuallyAsync(() => Calls("playerPreview.show").Should().Be(1));
    }

    [Fact]
    public async Task A_failure_loading_the_images_does_not_propagate_and_the_next_render_retries()
    {
        var cut = await RenderMapAsync();
        _factory.ListFailure = new InvalidOperationException("provocado");

        var click = () => Button(cut, "Vista de jugadores").Click();
        click.Should().NotThrow();
        await Task.Delay(200);
        Calls("playerPreview.show").Should().Be(0);

        _factory.ListFailure = null;
        Session.Mutate(e => { e.SetGrid(new GridSettings(true, 80, 0, 0)); return true; });

        await EventuallyAsync(() => Calls("playerPreview.show").Should().Be(1));
    }

    [Fact]
    public async Task An_image_change_in_the_vault_calls_update()
    {
        var cut = await RenderMapAsync();
        Button(cut, "Vista de jugadores").Click();
        await EventuallyAsync(() => Calls("playerPreview.show").Should().Be(1));

        File.WriteAllBytes(_vault.PathOf("_assets/nueva.png"), [1, 2, 3]);

        await EventuallyAsync(() => Calls("playerPreview.update").Should().BeGreaterThan(0));
    }

    [Fact]
    public async Task A_vault_change_calls_update()
    {
        var cut = await RenderMapAsync();
        Button(cut, "Vista de jugadores").Click();
        await EventuallyAsync(() => Calls("playerPreview.show").Should().Be(1));

        File.WriteAllText(_vault.PathOf("nota-nueva.md"), "x");

        await EventuallyAsync(() => Calls("playerPreview.update").Should().BeGreaterThan(0));
    }
}
