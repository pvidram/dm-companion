using DmCompanion.Screens.Tests.Support;
using System.Text.Json;
using Bunit;
using DmCompanion.Core.Vault.Storage;
using DmCompanion.Screens.Components;
using DmCompanion.Screens.Platform;
using DmCompanion.Screens.Services;
using DmCompanion.UI;
using Microsoft.Extensions.DependencyInjection;

namespace DmCompanion.Screens.Tests;

public sealed class MapCanvasTests : BunitContext, IAsyncLifetime
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly TempVault _vault = TempVault.Empty();
    private readonly InMemoryAppPreferences _preferences = new();
    private readonly CountingFactory _factory = new();

    public MapCanvasTests()
    {
        // Modo estricto: cualquier llamada JS no prevista falla el test.
        Services.AddDmCompanionUI();
        Services.AddSingleton<IAppPreferences>(_preferences);
        Services.AddSingleton<IVaultServiceFactory>(_factory);
        Services.AddSingleton<VaultHost>();
        Services.AddSingleton(TimeProvider.System);
        Services.AddSingleton<MapSession>();
        Services.AddSingleton<TokenHitPoints>();
        Services.AddSingleton<CombatController>();
        Services.AddSingleton<MapLibrary>();
        Services.AddSingleton<VaultFileUrls>();
        Services.AddSingleton<NoteImageResolver>();
        Services.AddSingleton<PortraitUrlProvider>();
        Services.AddScoped<ShellState>();
        Services.AddGlossaryForTests();
        Services.AddSoundForTests();
        Services.AddScoped<MapCanvasInterop>();
        Services.AddScoped<TurnStripInterop>();
        Services.AddScoped<DragPanelInterop>();

        JSInterop.SetupVoid("mapCanvas.load", _ => true).SetVoidResult();
        JSInterop.SetupVoid("mapCanvas.render", _ => true).SetVoidResult();
        JSInterop.SetupVoid("mapCanvas.dispose", _ => true).SetVoidResult();
    }

    Task IAsyncLifetime.InitializeAsync() => Task.CompletedTask;

    async Task IAsyncLifetime.DisposeAsync()
    {
        await Services.GetRequiredService<VaultHost>().DisposeAsync();
        await base.DisposeAsync();
        _vault.Dispose();
    }

    private MapSession Session => Services.GetRequiredService<MapSession>();

    private async Task OpenVaultAsync() => await Services.GetRequiredService<VaultHost>().OpenAsync(_vault.Root);

    private int Calls(string identifier) => JSInterop.Invocations.Count(i => i.Identifier == identifier);

    [Fact]
    public async Task Without_a_map_it_shows_the_empty_state_and_does_not_load_the_canvas()
    {
        await OpenVaultAsync();

        var cut = Render<MapCanvas>();

        cut.Markup.Should().Contain("Abrir o crear un mapa");
        Calls("mapCanvas.load").Should().Be(0);
    }

    [Fact]
    public async Task With_a_map_it_loads_the_canvas_with_a_vault_image_url()
    {
        MapTestVault.WriteImageMap(_vault, MapTestVault.StateWithGrid(true));
        await OpenVaultAsync();
        await Session.OpenAsync(MapTestVault.MapNote);

        var cut = Render<MapCanvas>();

        cut.WaitForAssertion(() => Calls("mapCanvas.load").Should().Be(1), Timeout);
        var dto = JsonSerializer.SerializeToElement(
            JSInterop.Invocations.Single(i => i.Identifier == "mapCanvas.load").Arguments[1], JsonSerializerOptions.Web);
        dto.GetProperty("imageUrl").GetString().Should().StartWith("/_vault/");
        cut.Markup.Should().NotContain("Abrir o crear un mapa");
    }

    [Fact]
    public async Task A_session_change_repaints_without_reloading()
    {
        MapTestVault.WriteImageMap(_vault, MapTestVault.StateWithGrid(false));
        await OpenVaultAsync();
        await Session.OpenAsync(MapTestVault.MapNote);
        var cut = Render<MapCanvas>();
        cut.WaitForAssertion(() => Calls("mapCanvas.load").Should().Be(1), Timeout);

        Session.Mutate(editor => editor.MoveToken("t1", 200, 200));

        cut.WaitForAssertion(() => Calls("mapCanvas.render").Should().BeGreaterThan(0), Timeout);
        Calls("mapCanvas.load").Should().Be(1);
    }

    [Fact]
    public async Task Opening_a_map_after_rendering_loads_the_canvas()
    {
        MapTestVault.WriteImageMap(_vault, MapTestVault.StateWithGrid(false));
        await OpenVaultAsync();
        var cut = Render<MapCanvas>();

        await Session.OpenAsync(MapTestVault.MapNote);

        cut.WaitForAssertion(() => Calls("mapCanvas.load").Should().Be(1), Timeout);
    }

    [Fact]
    public async Task Closing_the_map_returns_to_the_empty_state_and_releases_the_canvas()
    {
        MapTestVault.WriteImageMap(_vault, MapTestVault.StateWithGrid(false));
        await OpenVaultAsync();
        await Session.OpenAsync(MapTestVault.MapNote);
        var cut = Render<MapCanvas>();
        cut.WaitForAssertion(() => Calls("mapCanvas.load").Should().Be(1), Timeout);

        await Session.CloseAsync();

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Abrir o crear un mapa"), Timeout);
        Calls("mapCanvas.dispose").Should().Be(1);
    }

    [Fact]
    public async Task The_shell_reopens_the_last_map_and_replaces_the_placeholder()
    {
        MapTestVault.WriteImageMap(_vault, MapTestVault.StateWithGrid(false));
        await OpenVaultAsync();
        _preferences.Set("maps.last:" + Services.GetRequiredService<VaultHost>().Current!.RootPath, MapTestVault.MapNote);

        var cut = Render<DmShell>();

        cut.WaitForAssertion(() => Calls("mapCanvas.load").Should().Be(1), Timeout);
        cut.Markup.Should().NotContain("El mapa llegará en la fase 2");
    }

    private async Task<IRenderedComponent<MapCanvas>> RenderOpenMapAsync()
    {
        MapTestVault.WriteImageMap(_vault, MapTestVault.StateWithGrid(false));
        await OpenVaultAsync();
        await Session.OpenAsync(MapTestVault.MapNote);
        var cut = Render<MapCanvas>();
        cut.WaitForAssertion(() => Calls("mapCanvas.load").Should().Be(1), Timeout);
        return cut;
    }

    [Fact]
    public async Task Save_indicator_appears_while_there_are_unsaved_changes()
    {
        var cut = await RenderOpenMapAsync();

        Session.Mutate(editor => editor.MoveToken("t1", 210, 210));

        cut.WaitForAssertion(() => cut.FindAll(".map-notices [role=status][aria-label='Guardando…']").Should().HaveCount(1), Timeout);
    }

    [Fact]
    public async Task Save_indicator_disappears_when_the_save_finishes()
    {
        var cut = await RenderOpenMapAsync();
        Session.Mutate(editor => editor.MoveToken("t1", 220, 220));
        cut.WaitForAssertion(() => cut.FindAll(".ds-save-indicator").Should().HaveCount(1), Timeout);

        await Session.FlushAsync();

        cut.WaitForAssertion(() => cut.FindAll(".ds-save-indicator").Should().BeEmpty(), Timeout);
    }

    private static string[] Notices(IRenderedComponent<MapCanvas> cut) =>
        [.. cut.FindAll(".ds-notice").Select(n => n.TextContent.Trim())];

    [Fact]
    public async Task Without_notices_no_InlineNotice_is_shown()
    {
        MapTestVault.WriteImageMap(_vault, MapTestVault.StateWithGrid(false));
        await OpenVaultAsync();
        await Session.OpenAsync(MapTestVault.MapNote);

        var cut = Render<MapCanvas>();

        cut.FindAll(".ds-notice").Should().BeEmpty();
    }

    [Fact]
    public async Task Notice_is_shown_as_a_warning()
    {
        MapTestVault.WriteImageMap(_vault, rawState: "no es json");
        await OpenVaultAsync();
        await Session.OpenAsync(MapTestVault.MapNote);

        var cut = Render<MapCanvas>();

        cut.FindAll(".ds-notice--warning").Should().ContainSingle().Which.TextContent.Should().Contain(Session.Notice!);
    }

    [Fact]
    public async Task ImageNotice_is_shown_when_reported_after_rendering()
    {
        MapTestVault.WriteImageMap(_vault, MapTestVault.StateWithGrid(false));
        await OpenVaultAsync();
        await Session.OpenAsync(MapTestVault.MapNote);
        var cut = Render<MapCanvas>();

        Session.ReportImageMissing();

        cut.WaitForAssertion(() => Notices(cut).Should().ContainSingle().Which.Should().Contain("No se encuentra la imagen del mapa"), Timeout);
    }

    [Fact]
    public async Task Map_notice_close_button_dismisses_it()
    {
        MapTestVault.WriteImageMap(_vault, rawState: "no es json");
        await OpenVaultAsync();
        await Session.OpenAsync(MapTestVault.MapNote);
        var cut = Render<MapCanvas>();

        cut.Find(".ds-notice--warning button[aria-label='Cerrar aviso']").Click();

        (Session.Notice, cut.FindAll(".ds-notice").Count).Should().Be((null, 0));
    }

    [Fact]
    public async Task Image_notice_close_button_dismisses_only_that_notice()
    {
        MapTestVault.WriteImageMap(_vault, rawState: "no es json");
        await OpenVaultAsync();
        await Session.OpenAsync(MapTestVault.MapNote);
        var cut = Render<MapCanvas>();
        Session.ReportImageMissing();
        cut.WaitForAssertion(() => Notices(cut).Should().HaveCount(2), Timeout);

        cut.FindAll(".ds-notice").Single(n => n.TextContent.Contains("imagen")).QuerySelector("button[aria-label='Cerrar aviso']")!.Click();

        (Session.ImageNotice, Session.Notice is not null).Should().Be((null, true));
    }

    [Fact]
    public async Task Save_failure_notice_has_no_close_button_and_keeps_the_discard_option()
    {
        MapTestVault.WriteImageMap(_vault, MapTestVault.StateWithGrid(false));
        await OpenVaultAsync();
        await Session.OpenAsync(MapTestVault.MapNote);
        var cut = Render<MapCanvas>();
        _factory.FailWrites = true;

        Session.Mutate(e => e.ReplaceState(st => st with { Grid = st.Grid with { Enabled = true } }));
        await Session.FlushAsync();

        cut.WaitForAssertion(() => Session.HasSaveFailure.Should().BeTrue(), Timeout);
        (cut.FindAll(".ds-notice--warning button[aria-label='Cerrar aviso']").Count,
            cut.FindAll("button").Count(b => b.TextContent.Contains("Descartar cambios del mapa"))).Should().Be((0, 1));
    }

    [Fact]
    public async Task Both_notices_are_shown_together()
    {
        MapTestVault.WriteImageMap(_vault, rawState: "no es json");
        await OpenVaultAsync();
        await Session.OpenAsync(MapTestVault.MapNote);
        var cut = Render<MapCanvas>();

        Session.ReportImageMissing();

        cut.WaitForAssertion(() => Notices(cut).Should().HaveCount(2), Timeout);
    }

    [Fact]
    public async Task Map_notices_are_inside_the_notices_stack()
    {
        MapTestVault.WriteImageMap(_vault, rawState: "no es json");
        await OpenVaultAsync();
        await Session.OpenAsync(MapTestVault.MapNote);
        var cut = Render<MapCanvas>();

        cut.WaitForAssertion(() => cut.FindAll(".map-notices > .map-canvas__notices").Should().ContainSingle(), Timeout);
    }

    [Fact]
    public async Task With_a_map_the_controls_are_shown()
    {
        MapTestVault.WriteImageMap(_vault, MapTestVault.StateWithGrid(false));
        await OpenVaultAsync();
        await Session.OpenAsync(MapTestVault.MapNote);

        Render<MapCanvas>().FindAll(".ds-map-toolbar").Should().ContainSingle();
    }

    [Fact]
    public async Task Without_a_map_there_are_no_controls()
    {
        await OpenVaultAsync();

        Render<MapCanvas>().FindAll(".ds-map-toolbar").Should().BeEmpty();
    }
}
