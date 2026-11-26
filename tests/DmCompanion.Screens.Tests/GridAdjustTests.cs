using DmCompanion.Screens.Tests.Support;
using Bunit;
using DmCompanion.Core.Combat;
using DmCompanion.Core.Maps;
using DmCompanion.Core.Vault.Storage;
using DmCompanion.Infrastructure;
using DmCompanion.Screens.Components;
using DmCompanion.Screens.Platform;
using DmCompanion.Screens.Services;
using DmCompanion.UI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace DmCompanion.Screens.Tests;

public sealed class GridAdjustTests : BunitContext, IAsyncLifetime
{
    private const string Settings = "Ajustes de cuadrícula";
    private const string Adjust = "Ajustar con los dedos";

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly TempVault _vault = TempVault.Empty();

    public GridAdjustTests()
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
        JSInterop.SetupVoid("mapCanvas.dispose", _ => true).SetVoidResult();
        JSInterop.SetupVoid("mapCanvas.beginGridAdjust", _ => true).SetVoidResult();
        JSInterop.SetupVoid("mapCanvas.render", _ => true).SetVoidResult();
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

    private async Task OpenMapAsync(MapState? state = null, string? rawState = null)
    {
        MapTestVault.WriteImageMap(_vault, state ?? MapTestVault.StateWithGrid(true), rawState);
        await Services.GetRequiredService<VaultHost>().OpenAsync(_vault.Root);
        await Session.OpenAsync(MapTestVault.MapNote);
        await Canvas.LoadAsync(default);
    }

    private void AdjustedTo(params double[] grid) =>
        JSInterop.Setup<double[]?>("mapCanvas.endGridAdjust", _ => true).SetResult(grid);

    private IRenderedComponent<MapControls> OpenPanel()
    {
        var cut = Render<MapControls>();
        cut.Find($"[aria-label='{Settings}']").Click();
        return cut;
    }

    private static AngleSharp.Dom.IElement AdjustButton(IRenderedComponent<MapControls> cut) =>
        cut.FindAll("button").Single(b => b.TextContent.Trim() == Adjust);

    private async Task<IRenderedComponent<GridAdjustBar>> AdjustingAsync()
    {
        var bar = Render<GridAdjustBar>();
        await Canvas.BeginGridAdjustAsync();
        bar.WaitForAssertion(() => bar.Find("[aria-label='Ajustando cuadrícula']"));
        return bar;
    }

    private static AngleSharp.Dom.IElement BarButton(IRenderedComponent<GridAdjustBar> bar, string text) =>
        bar.FindAll("button").Single(b => b.TextContent.Trim() == text);

    [Fact]
    public async Task The_adjust_button_is_in_the_grid_panel_and_enabled()
    {
        await OpenMapAsync();

        AdjustButton(OpenPanel()).HasAttribute("disabled").Should().BeFalse();
    }

    [Fact]
    public async Task The_adjust_button_is_disabled_during_combat()
    {
        await OpenMapAsync();
        Services.GetRequiredService<CombatController>().Start([new InitiativeEntry("t1", 10)]);

        AdjustButton(OpenPanel()).HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public async Task The_adjust_button_is_disabled_with_the_grid_off()
    {
        await OpenMapAsync(MapTestVault.StateWithGrid(false));

        AdjustButton(OpenPanel()).HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public async Task Read_only_does_not_start_the_adjustment()
    {
        await OpenMapAsync(rawState: "esto no es json");

        await Canvas.BeginGridAdjustAsync();

        Canvas.GridAdjusting.Should().BeFalse();
    }

    [Fact]
    public async Task Pressing_the_adjust_button_enters_the_mode()
    {
        await OpenMapAsync();
        var cut = OpenPanel();

        AdjustButton(cut).Click();

        cut.WaitForAssertion(() => Canvas.GridAdjusting.Should().BeTrue());
    }

    [Fact]
    public async Task Pressing_the_adjust_button_closes_the_panel()
    {
        await OpenMapAsync();
        var cut = OpenPanel();

        AdjustButton(cut).Click();

        cut.WaitForAssertion(() => cut.FindAll("[aria-label='Ajustes de la cuadrícula']").Should().BeEmpty());
    }

    [Fact]
    public async Task The_toolbar_is_hidden_while_adjusting_and_comes_back_after()
    {
        await OpenMapAsync();
        var cut = Render<MapControls>();
        cut.FindAll("[aria-label='Herramientas del mapa']").Should().HaveCount(1);
        await Canvas.BeginGridAdjustAsync();
        cut.WaitForAssertion(() => cut.FindAll("[aria-label='Herramientas del mapa']").Should().BeEmpty());
        AdjustedTo(70, 0, 0);

        await Canvas.FinishGridAdjustAsync(false);

        cut.WaitForAssertion(() => cut.FindAll("[aria-label='Herramientas del mapa']").Should().HaveCount(1));
    }

    [Fact]
    public async Task Hiding_the_toolbar_for_the_adjustment_keeps_the_saved_preference()
    {
        await OpenMapAsync();
        var prefs = Services.GetRequiredService<IAppPreferences>();
        var cut = Render<MapControls>();
        await Canvas.BeginGridAdjustAsync();
        AdjustedTo(70, 0, 0);
        await Canvas.FinishGridAdjustAsync(false);

        cut.WaitForAssertion(() => cut.FindAll("[aria-label='Herramientas del mapa']").Should().HaveCount(1));
        prefs.Get("map.toolbar.visible").Should().NotBe("false");
    }

    [Fact]
    public async Task Pressing_the_adjust_button_gives_the_canvas_the_cell_limits()
    {
        await OpenMapAsync();

        AdjustButton(OpenPanel()).Click();

        JSInterop.VerifyInvoke("mapCanvas.beginGridAdjust").Arguments.Should().Equal(20.0, 200.0);
    }

    [Fact]
    public async Task The_strip_appears_in_adjust_mode_with_done_and_cancel()
    {
        await OpenMapAsync();

        var bar = await AdjustingAsync();

        bar.FindAll("button").Select(b => b.TextContent.Trim()).Should().Equal("Hecho", "Cancelar");
    }

    [Fact]
    public async Task There_is_no_strip_outside_adjust_mode()
    {
        await OpenMapAsync();

        Render<GridAdjustBar>().Markup.Trim().Should().BeEmpty();
    }

    [Fact]
    public async Task Done_saves_the_received_grid_in_a_single_mutation()
    {
        await OpenMapAsync(MapTestVault.StateWithGrid(true) with { Grid = new GridSettings(true, 70, 0, 0) });
        var bar = await AdjustingAsync();
        AdjustedTo(100, 12.5, 30);
        var changes = 0;
        Session.Changed += (_, _) => changes++;

        BarButton(bar, "Hecho").Click();

        bar.WaitForAssertion(() => Session.Editor!.State.Grid.Should().Be(new GridSettings(true, 100, 12.5, 30)));
        changes.Should().Be(1);
    }

    [Fact]
    public async Task Done_leaves_the_mode()
    {
        await OpenMapAsync();
        var bar = await AdjustingAsync();
        AdjustedTo(70, 0, 0);

        BarButton(bar, "Hecho").Click();

        bar.WaitForAssertion(() => Canvas.GridAdjusting.Should().BeFalse());
    }

    [Fact]
    public async Task Cancel_does_not_save()
    {
        var before = new GridSettings(true, 70, 5, 6);
        await OpenMapAsync(MapTestVault.StateWithGrid(true) with { Grid = before });
        var bar = await AdjustingAsync();
        AdjustedTo(150, 1, 1);

        BarButton(bar, "Cancelar").Click();

        bar.WaitForAssertion(() => Canvas.GridAdjusting.Should().BeFalse());
        Session.Editor!.State.Grid.Should().Be(before);
    }

    [Fact]
    public async Task Done_rounds_the_grid_to_one_decimal()
    {
        await OpenMapAsync(MapTestVault.StateWithGrid(true) with { Grid = new GridSettings(true, 70, 0, 0) });
        var bar = await AdjustingAsync();
        AdjustedTo(73.28461538, 12.449, 0.06);

        BarButton(bar, "Hecho").Click();

        bar.WaitForAssertion(() => Session.Editor!.State.Grid.Should().Be(new GridSettings(true, 73.3, 12.4, 0.1)));
    }

    [Fact]
    public async Task A_rejected_done_repaints_the_real_grid()
    {
        await OpenMapAsync(MapTestVault.StateWithGrid(true) with { Grid = new GridSettings(true, 70, 5, 6) });
        var bar = await AdjustingAsync();
        AdjustedTo(500, 1, 1);

        BarButton(bar, "Hecho").Click();

        bar.WaitForAssertion(() => JSInterop.VerifyInvoke("mapCanvas.render"));
    }

    [Fact]
    public async Task Cancel_does_not_repaint_from_csharp()
    {
        await OpenMapAsync();
        var bar = await AdjustingAsync();
        AdjustedTo(150, 1, 1);

        BarButton(bar, "Cancelar").Click();

        bar.WaitForAssertion(() => Canvas.GridAdjusting.Should().BeFalse());
        JSInterop.Invocations.Where(i => i.Identifier == "mapCanvas.render").Should().BeEmpty();
    }

    [Fact]
    public async Task The_adjustment_does_not_start_during_combat()
    {
        await OpenMapAsync();
        Services.GetRequiredService<CombatController>().Start([new InitiativeEntry("t1", 10)]);

        await Canvas.BeginGridAdjustAsync();

        Canvas.GridAdjusting.Should().BeFalse();
    }

    [Fact]
    public async Task Going_read_only_during_the_adjustment_cancels_it()
    {
        await OpenMapAsync();
        AdjustedTo(70, 0, 0);
        var bar = await AdjustingAsync();
        var future = MapStateSerializer.Write(Session.Editor!.State).Replace("\"version\":2", "\"version\":5");
        var statePath = _vault.PathOf(MapPaths.StatePathFor(MapTestVault.MapNote));

        File.WriteAllText(statePath, future);
        File.SetLastWriteTimeUtc(statePath, DateTime.UtcNow.AddMinutes(1));

        bar.WaitForAssertion(() => Canvas.GridAdjusting.Should().BeFalse(), Timeout);
        Session.IsReadOnly.Should().BeTrue();
    }

    [Fact]
    public async Task Done_ignores_a_grid_outside_the_cell_limits()
    {
        var before = new GridSettings(true, 70, 5, 6);
        await OpenMapAsync(MapTestVault.StateWithGrid(true) with { Grid = before });
        var bar = await AdjustingAsync();
        AdjustedTo(500, 1, 1);

        BarButton(bar, "Hecho").Click();

        bar.WaitForAssertion(() => Canvas.GridAdjusting.Should().BeFalse());
        Session.Editor!.State.Grid.Should().Be(before);
    }

    [Fact]
    public async Task A_change_of_the_map_state_from_elsewhere_cancels_the_adjustment()
    {
        await OpenMapAsync();
        AdjustedTo(70, 0, 0);
        var bar = await AdjustingAsync();

        Session.Mutate(editor => { editor.SetGrid(editor.State.Grid with { OffsetX = 3 }); return true; });

        bar.WaitForAssertion(() => Canvas.GridAdjusting.Should().BeFalse());
    }

    // Caso real en la tablet: se pulsa «Ajustar» antes de que termine el guardado diferido de un cambio anterior.
    [Fact]
    public async Task A_save_finishing_during_the_adjustment_does_not_cancel_it()
    {
        await OpenMapAsync();
        Session.Mutate(editor => { editor.SetGrid(editor.State.Grid with { OffsetX = 3 }); return true; });
        await Canvas.RenderAsync();
        var bar = await AdjustingAsync();
        var changes = 0;
        Session.Changed += (_, _) => changes++;

        await Session.FlushAsync();

        (changes, Canvas.GridAdjusting).Should().Be((1, true));
    }

    [Fact]
    public async Task Starting_a_combat_cancels_the_adjustment()
    {
        await OpenMapAsync();
        AdjustedTo(70, 0, 0);
        var bar = await AdjustingAsync();

        Services.GetRequiredService<CombatController>().Start([new InitiativeEntry("t1", 10)]);

        bar.WaitForAssertion(() => Canvas.GridAdjusting.Should().BeFalse());
    }

    [Fact]
    public async Task Unloading_the_canvas_cancels_the_adjustment()
    {
        await OpenMapAsync();
        await Canvas.BeginGridAdjustAsync();

        await Canvas.UnloadAsync();

        Canvas.GridAdjusting.Should().BeFalse();
    }
}
