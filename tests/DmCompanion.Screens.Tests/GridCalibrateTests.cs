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

public sealed class GridCalibrateTests : BunitContext, IAsyncLifetime
{
    private const string Calibrate = "Calibrar con un rectángulo";

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly TempVault _vault = TempVault.Empty();

    public GridCalibrateTests()
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
        JSInterop.SetupVoid("mapCanvas.beginGridCalibrate", _ => true).SetVoidResult();
        JSInterop.SetupVoid("mapCanvas.setCalibrateCells", _ => true).SetVoidResult();
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
        cut.Find("[aria-label='Ajustes de cuadrícula']").Click();
        return cut;
    }

    private static AngleSharp.Dom.IElement CalibrateButton(IRenderedComponent<MapControls> cut) =>
        cut.FindAll("button").Single(b => b.TextContent.Trim() == Calibrate);

    private async Task<IRenderedComponent<GridCalibrateBar>> CalibratingAsync()
    {
        var bar = Render<GridCalibrateBar>();
        await Canvas.BeginGridCalibrateAsync();
        bar.WaitForAssertion(() => bar.Find("[aria-label='Calibrando cuadrícula']"));
        return bar;
    }

    private async Task ReportAsync(IRenderedComponent<GridCalibrateBar> bar, double size)
    {
        await bar.InvokeAsync(() => Canvas.OnCalibrateChanged(size));
    }

    private static AngleSharp.Dom.IElement BarButton(IRenderedComponent<GridCalibrateBar> bar, string text) =>
        bar.FindAll("button").Single(b => b.TextContent.Trim() == text);

    private static AngleSharp.Dom.IElement Field(IRenderedComponent<GridCalibrateBar> bar, string label) =>
        bar.Find($"input[aria-label='{label}']");

    [Fact]
    public async Task The_calibrate_button_is_in_the_grid_panel_and_enabled()
    {
        await OpenMapAsync();

        CalibrateButton(OpenPanel()).HasAttribute("disabled").Should().BeFalse();
    }

    [Fact]
    public async Task The_calibrate_button_is_enabled_with_the_grid_off()
    {
        await OpenMapAsync(MapTestVault.StateWithGrid(false));

        CalibrateButton(OpenPanel()).HasAttribute("disabled").Should().BeFalse();
    }

    [Fact]
    public async Task The_calibrate_button_is_disabled_during_combat()
    {
        await OpenMapAsync();
        Services.GetRequiredService<CombatController>().Start([new InitiativeEntry("t1", 10)]);

        CalibrateButton(OpenPanel()).HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public async Task Pressing_the_calibrate_button_enters_calibrate_mode()
    {
        await OpenMapAsync();
        var cut = OpenPanel();

        await cut.InvokeAsync(() => CalibrateButton(cut).Click());

        cut.WaitForAssertion(() => (Canvas.GridCalibrating && Canvas.GridAdjusting).Should().BeTrue());
    }

    [Fact]
    public async Task Pressing_the_calibrate_button_closes_the_panel()
    {
        await OpenMapAsync();
        var cut = OpenPanel();

        await cut.InvokeAsync(() => CalibrateButton(cut).Click());

        cut.WaitForAssertion(() => cut.FindAll("[aria-label='Ajustes de la cuadrícula']").Should().BeEmpty());
    }

    [Fact]
    public async Task Calibrate_gives_the_canvas_the_cell_limits()
    {
        await OpenMapAsync();

        await Canvas.BeginGridCalibrateAsync();

        JSInterop.VerifyInvoke("mapCanvas.beginGridCalibrate").Arguments.Should().Equal(20.0, 200.0);
    }

    [Fact]
    public async Task The_bar_shows_columns_and_rows_starting_at_one()
    {
        await OpenMapAsync();

        var bar = await CalibratingAsync();

        new[] { Field(bar, "Columnas").GetAttribute("value"), Field(bar, "Filas").GetAttribute("value") }.Should().Equal("1", "1");
    }

    [Fact]
    public async Task The_bar_shows_done_and_cancel()
    {
        await OpenMapAsync();

        var bar = await CalibratingAsync();

        bar.FindAll("button").Select(b => b.TextContent.Trim()).Should().Contain(["Hecho", "Cancelar"]);
    }

    [Fact]
    public async Task The_adjust_bar_is_not_shown_while_calibrating()
    {
        await OpenMapAsync();
        var adjust = Render<GridAdjustBar>();

        await Canvas.BeginGridCalibrateAsync();

        adjust.FindAll("[aria-label='Ajustando cuadrícula']").Should().BeEmpty();
    }

    [Fact]
    public async Task The_calibrate_bar_is_not_shown_outside_calibration()
    {
        await OpenMapAsync();

        Render<GridCalibrateBar>().FindAll("[aria-label='Calibrando cuadrícula']").Should().BeEmpty();
    }

    [Fact]
    public async Task Changing_columns_and_rows_tells_the_canvas()
    {
        await OpenMapAsync();
        var bar = await CalibratingAsync();

        await bar.InvokeAsync(() => Field(bar, "Columnas").Change("3"));
        await bar.InvokeAsync(() => Field(bar, "Filas").Change("2"));

        JSInterop.Invocations["mapCanvas.setCalibrateCells"].Last().Arguments.Should().Equal(3, 2);
    }

    [Fact]
    public async Task A_new_calibration_starts_again_at_1_by_1()
    {
        await OpenMapAsync();
        var bar = await CalibratingAsync();
        await bar.InvokeAsync(() => Field(bar, "Columnas").Change("3"));
        await bar.InvokeAsync(() => Field(bar, "Filas").Change("2"));
        AdjustedTo(50, 0, 0);
        await bar.InvokeAsync(() => BarButton(bar, "Cancelar").Click());
        bar.WaitForAssertion(() => Canvas.GridCalibrating.Should().BeFalse());

        await Canvas.BeginGridCalibrateAsync();

        bar.WaitForAssertion(() => new[] { Field(bar, "Columnas").GetAttribute("value"), Field(bar, "Filas").GetAttribute("value") }.Should().Equal("1", "1"));
    }

    [Fact]
    public async Task Invalid_columns_are_not_sent_to_the_canvas()
    {
        await OpenMapAsync();
        await Canvas.BeginGridCalibrateAsync();

        await Canvas.SetCalibrateCellsAsync(0, 2);

        JSInterop.Invocations["mapCanvas.setCalibrateCells"].Should().BeEmpty();
    }

    [Fact]
    public async Task Done_is_disabled_until_a_rectangle_exists()
    {
        await OpenMapAsync();

        var bar = await CalibratingAsync();

        BarButton(bar, "Hecho").HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public async Task Done_is_enabled_once_the_canvas_reports_a_valid_size()
    {
        await OpenMapAsync();
        var bar = await CalibratingAsync();

        await ReportAsync(bar, 50);

        bar.WaitForAssertion(() => BarButton(bar, "Hecho").HasAttribute("disabled").Should().BeFalse());
    }

    [Theory]
    [InlineData(250)]
    [InlineData(10)]
    public async Task An_out_of_range_size_shows_a_warning(double size)
    {
        await OpenMapAsync();
        var bar = await CalibratingAsync();

        await ReportAsync(bar, size);

        bar.WaitForAssertion(() => bar.Find("[role='alert']").TextContent.Should().Contain(size.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)));
    }

    [Theory]
    [InlineData(250)]
    [InlineData(10)]
    public async Task An_out_of_range_size_disables_done(double size)
    {
        await OpenMapAsync();
        var bar = await CalibratingAsync();

        await ReportAsync(bar, size);

        bar.WaitForAssertion(() => BarButton(bar, "Hecho").HasAttribute("disabled").Should().BeTrue());
    }

    [Fact]
    public async Task Done_saves_the_calibrated_grid()
    {
        await OpenMapAsync(MapTestVault.StateWithGrid(true) with { Grid = new GridSettings(true, 70, 0, 0) });
        var bar = await CalibratingAsync();
        AdjustedTo(50.04, 12.26, 7.0);
        await ReportAsync(bar, 50);

        await bar.InvokeAsync(() => BarButton(bar, "Hecho").Click());

        bar.WaitForAssertion(() => Session.Editor!.State.Grid.Should().Be(new GridSettings(true, 50.0, 12.3, 7.0)));
    }

    [Fact]
    public async Task Done_clips_the_offset_against_the_rounded_size()
    {
        await OpenMapAsync(MapTestVault.StateWithGrid(true) with { Grid = new GridSettings(true, 70, 0, 0) });
        var bar = await CalibratingAsync();
        AdjustedTo(49.94, 49.97, 10.0);
        await ReportAsync(bar, 49.94);

        await bar.InvokeAsync(() => BarButton(bar, "Hecho").Click());

        bar.WaitForAssertion(() => Session.Editor!.State.Grid.Should().Be(new GridSettings(true, 49.9, 0.1, 10.0)));
    }

    [Fact]
    public async Task Done_turns_the_grid_on_when_it_was_off()
    {
        await OpenMapAsync(MapTestVault.StateWithGrid(false));
        var bar = await CalibratingAsync();
        AdjustedTo(50, 0, 0);
        await ReportAsync(bar, 50);

        await bar.InvokeAsync(() => BarButton(bar, "Hecho").Click());

        bar.WaitForAssertion(() => Session.Editor!.State.Grid.Enabled.Should().BeTrue());
    }

    [Fact]
    public async Task Done_without_a_rectangle_changes_nothing()
    {
        var before = new GridSettings(true, 70, 5, 6);
        await OpenMapAsync(MapTestVault.StateWithGrid(true) with { Grid = before });
        await Canvas.BeginGridCalibrateAsync();
        AdjustedTo(50, 0, 0);

        await Canvas.FinishGridAdjustAsync(true);

        Session.Editor!.State.Grid.Should().Be(before);
    }

    [Fact]
    public async Task Done_without_a_rectangle_leaves_the_mode()
    {
        await OpenMapAsync();
        await Canvas.BeginGridCalibrateAsync();
        AdjustedTo(50, 0, 0);

        await Canvas.FinishGridAdjustAsync(true);

        Canvas.GridCalibrating.Should().BeFalse();
    }

    [Fact]
    public async Task Done_rejects_a_size_outside_the_limits()
    {
        var before = new GridSettings(true, 70, 5, 6);
        await OpenMapAsync(MapTestVault.StateWithGrid(true) with { Grid = before });
        var bar = await CalibratingAsync();
        AdjustedTo(250, 0, 0);
        await ReportAsync(bar, 250);

        await Canvas.FinishGridAdjustAsync(true);

        Session.Editor!.State.Grid.Should().Be(before);
    }

    [Fact]
    public async Task A_rejected_done_repaints_the_real_grid()
    {
        await OpenMapAsync(MapTestVault.StateWithGrid(true) with { Grid = new GridSettings(true, 70, 5, 6) });
        var bar = await CalibratingAsync();
        AdjustedTo(250, 0, 0);
        await ReportAsync(bar, 250);

        await Canvas.FinishGridAdjustAsync(true);

        JSInterop.VerifyInvoke("mapCanvas.render");
    }

    [Fact]
    public async Task Cancel_does_not_save()
    {
        var before = new GridSettings(true, 70, 5, 6);
        await OpenMapAsync(MapTestVault.StateWithGrid(true) with { Grid = before });
        var bar = await CalibratingAsync();
        AdjustedTo(50, 0, 0);
        await ReportAsync(bar, 50);

        await bar.InvokeAsync(() => BarButton(bar, "Cancelar").Click());

        Session.Editor!.State.Grid.Should().Be(before);
    }

    [Fact]
    public async Task Cancel_leaves_the_mode()
    {
        await OpenMapAsync();
        var bar = await CalibratingAsync();
        AdjustedTo(50, 0, 0);

        await bar.InvokeAsync(() => BarButton(bar, "Cancelar").Click());

        bar.WaitForAssertion(() => Canvas.GridCalibrating.Should().BeFalse());
    }

    [Fact]
    public async Task Starting_combat_cancels_the_calibration()
    {
        await OpenMapAsync();
        var bar = await CalibratingAsync();
        AdjustedTo(50, 0, 0);

        Services.GetRequiredService<CombatController>().Start([new InitiativeEntry("t1", 10)]);

        bar.WaitForAssertion(() => Canvas.GridCalibrating.Should().BeFalse(), Timeout);
    }

    [Fact]
    public async Task Going_read_only_cancels_the_calibration()
    {
        await OpenMapAsync();
        var bar = await CalibratingAsync();
        AdjustedTo(50, 0, 0);
        var future = MapStateSerializer.Write(Session.Editor!.State).Replace("\"version\":2", "\"version\":4");
        var statePath = _vault.PathOf(MapPaths.StatePathFor(MapTestVault.MapNote));

        File.WriteAllText(statePath, future);
        File.SetLastWriteTimeUtc(statePath, DateTime.UtcNow.AddMinutes(1));

        bar.WaitForAssertion(() => Canvas.GridCalibrating.Should().BeFalse(), Timeout);
    }

    [Fact]
    public async Task The_calibration_does_not_start_in_read_only()
    {
        await OpenMapAsync(rawState: "esto no es json");

        await Canvas.BeginGridCalibrateAsync();

        Canvas.GridCalibrating.Should().BeFalse();
    }
}
