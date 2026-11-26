using Bunit;
using DmCompanion.Core.Combat;
using DmCompanion.Core.Maps;
using DmCompanion.Core.Vault.Storage;
using DmCompanion.Infrastructure;
using DmCompanion.Screens.Components;
using DmCompanion.Screens.Platform;
using DmCompanion.Screens.Services;
using DmCompanion.Screens.Tests.Support;
using DmCompanion.UI;
using DmCompanion.UI.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace DmCompanion.Screens.Tests;

public sealed class MapControlsFogTests : BunitContext, IAsyncLifetime
{
    private readonly TempVault _vault = TempVault.Empty();

    public MapControlsFogTests()
    {
        Services.AddDmCompanionUI();
        Services.AddSingleton<IAppPreferences>(new InMemoryAppPreferences());
        Services.AddSingleton<IVaultServiceFactory>(new FileSystemVaultServiceFactory(NullLoggerFactory.Instance));
        Services.AddSingleton<VaultHost>();
        Services.AddSingleton<TimeProvider>(new FakeTimeProvider());
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

        JSInterop.SetupVoid("mapCanvas.load", _ => true).SetVoidResult();
        JSInterop.SetupVoid("mapCanvas.setMode", _ => true).SetVoidResult();
        JSInterop.SetupVoid("mapCanvas.setFogTool", _ => true).SetVoidResult();
        JSInterop.SetupVoid("mapCanvas.fogDraft", _ => true).SetVoidResult();
        JSInterop.SetupVoid("mapCanvas.fit", _ => true).SetVoidResult();
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
    private MapCanvasInterop Canvas => Services.GetRequiredService<MapCanvasInterop>();

    private async Task OpenMapAsync(string? rawState = null)
    {
        MapTestVault.WriteImageMap(_vault, rawState is null ? MapTestVault.StateWithGrid(false) : null, rawState);
        await Services.GetRequiredService<VaultHost>().OpenAsync(_vault.Root);
        await Session.OpenAsync(MapTestVault.MapNote);
    }

    private static AngleSharp.Dom.IElement Button(IRenderedComponent<MapControls> cut, string label) =>
        cut.Find($"[aria-label='{label}']");

    private static bool Has(IRenderedComponent<MapControls> cut, string label) => cut.FindAll($"[aria-label='{label}']").Count > 0;

    private static bool Pressed(IRenderedComponent<MapControls> cut, string label) =>
        Button(cut, label).GetAttribute("aria-pressed") == "true";

    private static List<string?> Names(IRenderedComponent<MapControls> cut) =>
        cut.FindAll("button").Select(b => b.GetAttribute("aria-label")).ToList();

    private async Task<IRenderedComponent<MapControls>> FogModeAsync()
    {
        await OpenMapAsync();
        await Canvas.LoadAsync(default);
        var cut = Render<MapControls>();
        Button(cut, "Niebla").Click();
        return cut;
    }

    [Fact]
    public async Task Fog_button_comes_right_after_Draw()
    {
        await OpenMapAsync();
        var names = Names(Render<MapControls>());

        names.IndexOf("Niebla").Should().Be(names.IndexOf("Dibujar") + 1);
    }

    [Fact]
    public async Task Fog_button_comes_right_before_Measure()
    {
        await OpenMapAsync();
        var names = Names(Render<MapControls>());

        names.IndexOf("Medir").Should().Be(names.IndexOf("Niebla") + 1);
    }

    [Fact]
    public async Task Read_only_hides_the_Fog_button()
    {
        await OpenMapAsync(rawState: "esto no es json");

        Has(Render<MapControls>(), "Niebla").Should().BeFalse();
    }

    [Fact]
    public async Task Pressing_Fog_sets_the_fog_mode()
    {
        await FogModeAsync();

        Canvas.Mode.Should().Be(MapMode.Fog);
    }

    [Fact]
    public async Task Fog_tools_are_hidden_outside_fog_mode()
    {
        await OpenMapAsync();

        Has(Render<MapControls>(), "Destapar").Should().BeFalse();
    }

    [Fact]
    public async Task Fog_mode_shows_the_three_tools()
    {
        var cut = await FogModeAsync();

        (Has(cut, "Destapar"), Has(cut, "Trazar"), Has(cut, "Goma")).Should().Be((true, true, true));
    }

    [Fact]
    public async Task Reveal_is_pressed_by_default()
    {
        var cut = await FogModeAsync();

        (Pressed(cut, "Destapar"), Pressed(cut, "Trazar"), Pressed(cut, "Goma")).Should().Be((true, false, false));
    }

    [Fact]
    public async Task Pressing_Trace_selects_the_trace_tool()
    {
        var cut = await FogModeAsync();

        Button(cut, "Trazar").Click();

        JSInterop.Invocations.Should().Contain(i => i.Identifier == "mapCanvas.setFogTool" && (string)i.Arguments[0]! == "Trace");
    }

    [Fact]
    public async Task Trace_stays_pressed_after_leaving_and_returning_to_fog()
    {
        var cut = await FogModeAsync();
        Button(cut, "Trazar").Click();

        Button(cut, "Navegar").Click();
        Button(cut, "Niebla").Click();

        Pressed(cut, "Trazar").Should().BeTrue();
    }

    [Fact]
    public async Task Draft_buttons_are_disabled_without_a_draft()
    {
        var cut = await FogModeAsync();
        Button(cut, "Trazar").Click();

        (Button(cut, "Deshacer esquina").HasAttribute("disabled"), Button(cut, "Cancelar").HasAttribute("disabled"))
            .Should().Be((true, true));
    }

    [Fact]
    public async Task Draft_buttons_are_enabled_once_there_are_corners()
    {
        var cut = await FogModeAsync();
        Button(cut, "Trazar").Click();

        Canvas.OnFogDraftChanged(2);

        cut.WaitForAssertion(() =>
            (Button(cut, "Deshacer esquina").HasAttribute("disabled"), Button(cut, "Cancelar").HasAttribute("disabled"))
                .Should().Be((false, false)));
    }

    [Fact]
    public async Task Draft_buttons_invoke_undo_and_cancel()
    {
        var cut = await FogModeAsync();
        Button(cut, "Trazar").Click();
        Canvas.OnFogDraftChanged(2);
        cut.WaitForAssertion(() => Button(cut, "Cancelar").HasAttribute("disabled").Should().BeFalse());

        Button(cut, "Deshacer esquina").Click();
        Button(cut, "Cancelar").Click();

        JSInterop.Invocations.Where(i => i.Identifier == "mapCanvas.fogDraft").Select(i => (string)i.Arguments[0]!)
            .Should().Equal("undo", "cancel");
    }

    [Fact]
    public async Task Draft_buttons_are_hidden_with_the_other_tools()
    {
        var cut = await FogModeAsync();

        Has(cut, "Deshacer esquina").Should().BeFalse();
    }

    [Fact]
    public async Task Map_undo_and_redo_are_present_in_fog_mode()
    {
        var cut = await FogModeAsync();

        (Has(cut, "Deshacer"), Has(cut, "Rehacer")).Should().Be((true, true));
    }

    [Fact]
    public async Task Map_undo_and_redo_follow_the_editor_history()
    {
        var cut = await FogModeAsync();
        Session.Mutate(e => e.AddFogZone([0, 0, 100, 0, 0, 100]) is not null);

        cut.WaitForAssertion(() =>
            (Button(cut, "Deshacer").HasAttribute("disabled"), Button(cut, "Rehacer").HasAttribute("disabled"))
                .Should().Be((false, true)));
    }

    [Fact]
    public async Task Reveal_has_its_own_icon_different_from_the_view_button()
    {
        var cut = await FogModeAsync();

        Button(cut, "Destapar").InnerHtml.Should().Contain(IconFiles.Url(IconName.ScanEye));
    }

    [Fact]
    public async Task Undo_corner_has_its_own_icon_different_from_map_undo()
    {
        var cut = await FogModeAsync();
        Button(cut, "Trazar").Click();

        Button(cut, "Deshacer esquina").InnerHtml.Should().Contain(IconFiles.Url(IconName.Delete));
    }

    private static AngleSharp.Dom.IElement FogGroupButton(IRenderedComponent<MapControls> cut, string label) =>
        cut.Find($"[role='group'][aria-label='Niebla'] [aria-label='{label}']");

    [Fact]
    public async Task Fog_mode_shows_Mano_alzada_and_Cambiar_tipo()
    {
        var cut = await FogModeAsync();

        cut.FindAll("[role='group'][aria-label='Niebla'] [aria-label='Mano alzada'], [role='group'][aria-label='Niebla'] [aria-label='Cambiar tipo']")
            .Count.Should().Be(2);
    }

    [Fact]
    public async Task Mano_alzada_comes_right_after_Trazar_in_the_fog_group()
    {
        var cut = await FogModeAsync();

        var names = cut.FindAll("[role='group'][aria-label='Niebla'] button").Select(b => b.GetAttribute("aria-label")).ToList();

        names.IndexOf("Mano alzada").Should().Be(names.IndexOf("Trazar") + 1);
    }

    [Fact]
    public async Task Pressing_Mano_alzada_selects_the_freehand_tool()
    {
        var cut = await FogModeAsync();

        await cut.InvokeAsync(() => FogGroupButton(cut, "Mano alzada").Click());

        JSInterop.Invocations.Should().Contain(i => i.Identifier == "mapCanvas.setFogTool" && (string)i.Arguments[0]! == "Freehand");
    }

    [Fact]
    public async Task Pressing_Cambiar_tipo_selects_the_retype_tool()
    {
        var cut = await FogModeAsync();

        await cut.InvokeAsync(() => FogGroupButton(cut, "Cambiar tipo").Click());

        JSInterop.Invocations.Should().Contain(i => i.Identifier == "mapCanvas.setFogTool" && (string)i.Arguments[0]! == "Retype");
    }

    [Fact]
    public async Task Mano_alzada_and_Trazar_use_different_icons()
    {
        var cut = await FogModeAsync();

        FogGroupButton(cut, "Mano alzada").InnerHtml.Should().Contain(IconFiles.Url(IconName.Spline))
            .And.NotContain(IconFiles.Url(IconName.Pencil));
    }

    [Fact]
    public async Task Cambiar_tipo_uses_its_own_icon_different_from_Destapar()
    {
        var cut = await FogModeAsync();

        FogGroupButton(cut, "Cambiar tipo").InnerHtml.Should().Contain(IconFiles.Url(IconName.ArrowLeftRight));
    }

    [Fact]
    public async Task Mano_alzada_is_pressed_when_it_is_the_active_tool()
    {
        var cut = await FogModeAsync();

        await cut.InvokeAsync(() => FogGroupButton(cut, "Mano alzada").Click());

        FogGroupButton(cut, "Mano alzada").GetAttribute("aria-pressed").Should().Be("true");
    }

    [Fact]
    public async Task Cambiar_tipo_is_pressed_when_it_is_the_active_tool()
    {
        var cut = await FogModeAsync();

        await cut.InvokeAsync(() => FogGroupButton(cut, "Cambiar tipo").Click());

        FogGroupButton(cut, "Cambiar tipo").GetAttribute("aria-pressed").Should().Be("true");
    }

    [Fact]
    public async Task Total_is_the_default_zone_type()
    {
        var cut = await FogModeAsync();

        (Pressed(cut, "Total"), Pressed(cut, "PNJ")).Should().Be((true, false));
    }

    [Fact]
    public async Task Pressing_PNJ_sets_the_type_of_new_zones_and_presses_the_button()
    {
        var cut = await FogModeAsync();

        await cut.InvokeAsync(() => Button(cut, "PNJ").Click());

        (Canvas.FogType, Pressed(cut, "PNJ")).Should().Be((FogType.Npc, true));
    }

    [Fact]
    public async Task The_chosen_type_survives_leaving_and_returning_to_fog_mode()
    {
        var cut = await FogModeAsync();
        await cut.InvokeAsync(() => Button(cut, "PNJ").Click());

        await cut.InvokeAsync(() => Button(cut, "Navegar").Click());
        await cut.InvokeAsync(() => Button(cut, "Niebla").Click());

        Pressed(cut, "PNJ").Should().BeTrue();
    }

    [Fact]
    public async Task The_corner_draft_buttons_do_not_appear_with_the_freehand_tool()
    {
        var cut = await FogModeAsync();

        await cut.InvokeAsync(() => FogGroupButton(cut, "Mano alzada").Click());

        Has(cut, "Deshacer esquina").Should().BeFalse();
    }
}
