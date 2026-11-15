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

namespace DmCompanion.Screens.Tests;

public sealed class MapControlsTests : BunitContext, IAsyncLifetime
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly TempVault _vault = TempVault.Empty();

    public MapControlsTests()
    {
        Services.AddDmCompanionUI();
        Services.AddSingleton<IAppPreferences>(new InMemoryAppPreferences());
        Services.AddSingleton<IVaultServiceFactory>(new FileSystemVaultServiceFactory(NullLoggerFactory.Instance));
        Services.AddSingleton<VaultHost>();
        Services.AddSingleton(TimeProvider.System);
        Services.AddSingleton<MapSession>();
        Services.AddSingleton<TokenHitPoints>();
        Services.AddSingleton<CombatController>();
        Services.AddSingleton<MapLibrary>();
        Services.AddSingleton<VaultFileUrls>();
        Services.AddSingleton<PortraitUrlProvider>();
        Services.AddScoped<ShellState>();
        Services.AddSoundForTests();
        Services.AddScoped<MapCanvasInterop>();
        Services.AddScoped<TurnStripInterop>();
        Services.AddScoped<DragPanelInterop>();

        JSInterop.SetupVoid("mapCanvas.load", _ => true).SetVoidResult();
        JSInterop.SetupVoid("mapCanvas.setMode", _ => true).SetVoidResult();
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

    private async Task OpenMapAsync(MapState? state = null, string? rawState = null)
    {
        MapTestVault.WriteImageMap(_vault, state ?? MapTestVault.StateWithGrid(false), rawState);
        await Services.GetRequiredService<VaultHost>().OpenAsync(_vault.Root);
        await Session.OpenAsync(MapTestVault.MapNote);
    }

    private static AngleSharp.Dom.IElement ByLabel(IRenderedComponent<MapControls> cut, string label) =>
        cut.Find($"[aria-label='{label}']");

    private static AngleSharp.Dom.IElement ButtonText(IRenderedComponent<MapControls> cut, string text) =>
        cut.FindAll("button").Single(b => b.GetAttribute("aria-label") == text || b.TextContent.Trim() == text);

    private static string[] ButtonNames(IRenderedComponent<MapControls> cut) =>
        [.. cut.FindAll("button").Select(b => b.GetAttribute("aria-label") ?? b.TextContent.Trim())];

    private static AngleSharp.Dom.IElement GridSwitch(IRenderedComponent<MapControls> cut) =>
        cut.Find("input[type=checkbox]");

    private static AngleSharp.Dom.IElement Stepper(IRenderedComponent<MapControls> cut, string label) =>
        cut.Find($"input[aria-label='{label} (px)']");

    private static bool Has(IRenderedComponent<MapControls> cut, string label) => cut.FindAll($"[aria-label='{label}']").Count > 0;

    private async Task<IRenderedComponent<MapControls>> DrawModeAsync()
    {
        await OpenMapAsync();
        var cut = Render<MapControls>();
        ButtonText(cut, "Dibujar").Click();
        return cut;
    }

    [Fact]
    public void Palette_has_six_colors_and_three_widths()
    {
        MapControls.Palette.Should().HaveCount(6).And.OnlyHaveUniqueItems();
        MapControls.Widths.Should().Equal(3, 6, 12);
    }

    [Fact]
    public async Task Toolbar_is_anchored_at_the_bottom_edge()
    {
        await OpenMapAsync();

        Render<MapControls>().Find(".ds-map-toolbar").ClassList.Should().Contain("ds-map-toolbar--bottom");
    }

    [Fact]
    public async Task Drawing_tools_are_hidden_outside_draw_mode()
    {
        await OpenMapAsync();

        Has(Render<MapControls>(), "Deshacer").Should().BeFalse();
    }

    [Fact]
    public async Task Draw_mode_shows_six_colors()
    {
        var cut = await DrawModeAsync();

        cut.FindAll("[aria-label^='Color ']").Should().HaveCount(6);
    }

    [Fact]
    public async Task Draw_mode_shows_three_widths()
    {
        var cut = await DrawModeAsync();

        cut.FindAll("[aria-label^='Grosor ']").Should().HaveCount(3);
    }

    [Theory]
    [InlineData("Goma")]
    [InlineData("Deshacer trazo")]
    [InlineData("Rehacer trazo")]
    public async Task Draw_mode_shows_the_stroke_tools(string label)
    {
        var cut = await DrawModeAsync();

        Has(cut, label).Should().BeTrue();
    }

    private async Task<(IRenderedComponent<MapControls> Cut, MapCanvasInterop Interop)> DrawnStrokeAsync()
    {
        var cut = await DrawModeAsync();
        var interop = Services.GetRequiredService<MapCanvasInterop>();
        interop.OnStrokeAdded("#000000", 6, [1, 2, 0.5, 3, 4, 0.5]);
        cut.WaitForAssertion(() => ByLabel(cut, "Deshacer trazo").HasAttribute("disabled").Should().BeFalse(), Timeout);
        return (cut, interop);
    }

    [Fact]
    public async Task Undo_and_redo_start_disabled()
    {
        var cut = await DrawModeAsync();

        ByLabel(cut, "Deshacer trazo").HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public async Task Only_the_chosen_color_and_the_chosen_width_are_pressed()
    {
        await OpenMapAsync();
        await Services.GetRequiredService<MapCanvasInterop>().LoadAsync(default);
        var cut = Render<MapControls>();
        ButtonText(cut, "Dibujar").Click();

        ByLabel(cut, "Color 3").Click();
        ByLabel(cut, "Grosor 12").Click();

        (cut.FindAll("[aria-label^='Color '][aria-pressed='true']").Select(b => b.GetAttribute("aria-label")),
            cut.FindAll("[aria-label^='Grosor '][aria-pressed='true']").Select(b => b.GetAttribute("aria-label")))
            .Should().BeEquivalentTo((new[] { "Color 3" }, new[] { "Grosor 12" }));
    }

    [Fact]
    public async Task Redo_starts_disabled()
    {
        var cut = await DrawModeAsync();

        ByLabel(cut, "Rehacer trazo").HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public async Task Undo_removes_the_last_stroke()
    {
        var (cut, _) = await DrawnStrokeAsync();

        ByLabel(cut, "Deshacer trazo").Click();

        Session.Editor!.State.Strokes.Should().BeEmpty();
    }

    [Fact]
    public async Task Undo_disables_itself_and_enables_redo_when_nothing_is_left()
    {
        var (cut, _) = await DrawnStrokeAsync();

        ByLabel(cut, "Deshacer trazo").Click();

        cut.WaitForAssertion(() => ByLabel(cut, "Deshacer trazo").HasAttribute("disabled").Should().BeTrue(), Timeout);
        ByLabel(cut, "Rehacer trazo").HasAttribute("disabled").Should().BeFalse();
    }

    [Fact]
    public async Task Redo_restores_the_undone_stroke()
    {
        var (cut, _) = await DrawnStrokeAsync();
        ByLabel(cut, "Deshacer trazo").Click();
        cut.WaitForAssertion(() => ByLabel(cut, "Rehacer trazo").HasAttribute("disabled").Should().BeFalse(), Timeout);

        ByLabel(cut, "Rehacer trazo").Click();

        Session.Editor!.State.Strokes.Should().ContainSingle();
    }

    [Fact]
    public async Task Choosing_a_color_and_a_width_updates_the_tool()
    {
        var cut = await DrawModeAsync();

        ByLabel(cut, "Grosor 12").Click();
        ByLabel(cut, "Color 3").Click();

        Services.GetRequiredService<MapCanvasInterop>().Tool.Should().Be(new DrawTool(MapControls.Palette[2], 12, false));
    }

    [Fact]
    public async Task The_eraser_turns_on_in_the_tool()
    {
        var cut = await DrawModeAsync();

        ByLabel(cut, "Goma").Click();

        Services.GetRequiredService<MapCanvasInterop>().Tool.Eraser.Should().BeTrue();
    }

    [Fact]
    public async Task The_eraser_button_shows_as_pressed()
    {
        var cut = await DrawModeAsync();

        ByLabel(cut, "Goma").Click();

        ByLabel(cut, "Goma").GetAttribute("aria-pressed").Should().Be("true");
    }

    [Fact]
    public async Task Choosing_a_color_turns_the_eraser_off()
    {
        var cut = await DrawModeAsync();
        ByLabel(cut, "Goma").Click();

        ByLabel(cut, "Color 1").Click();

        Services.GetRequiredService<MapCanvasInterop>().Tool.Eraser.Should().BeFalse();
    }

    [Fact]
    public async Task Mode_buttons_remember_the_mode()
    {
        await OpenMapAsync();
        await Services.GetRequiredService<MapCanvasInterop>().LoadAsync(default);
        var cut = Render<MapControls>();

        ButtonText(cut, "Medir").Click();

        Services.GetRequiredService<MapCanvasInterop>().Mode.Should().Be(MapMode.Measure);
    }

    [Fact]
    public async Task Mode_buttons_send_the_mode_to_the_canvas_js()
    {
        await OpenMapAsync();
        await Services.GetRequiredService<MapCanvasInterop>().LoadAsync(default);
        var cut = Render<MapControls>();

        ButtonText(cut, "Medir").Click();

        JSInterop.Invocations.Should().Contain(i => i.Identifier == "mapCanvas.setMode" && (string)i.Arguments[0]! == "Measure");
    }

    [Fact]
    public async Task Changing_the_cell_size_calls_SetGrid_and_gets_saved()
    {
        await OpenMapAsync();
        var cut = Render<MapControls>();
        ByLabel(cut, "Ajustes de cuadrícula").Click();

        Stepper(cut, "Tamaño de casilla").Change("100");

        Session.Editor!.State.Grid.CellSize.Should().Be(100);
        await Session.FlushAsync();
        var saved = MapStateSerializer.Read(File.ReadAllText(_vault.PathOf(MapPaths.StatePathFor(MapTestVault.MapNote))));
        saved.State.Grid.CellSize.Should().Be(100);
    }

    [Fact]
    public async Task Shrinking_the_cell_below_the_offset_clamps_the_offset()
    {
        await OpenMapAsync(MapTestVault.StateWithGrid(true) with { Grid = new GridSettings(true, 100, 90, 0) });
        var cut = Render<MapControls>();
        ByLabel(cut, "Ajustes de cuadrícula").Click();

        Stepper(cut, "Tamaño de casilla").Change("40");

        Session.Editor!.State.Grid.Should().Be(new GridSettings(true, 40, 40, 0));
    }

    [Fact]
    public async Task The_cell_size_is_limited_to_20_to_200()
    {
        await OpenMapAsync();
        var cut = Render<MapControls>();
        ByLabel(cut, "Ajustes de cuadrícula").Click();

        Stepper(cut, "Tamaño de casilla").Change("500");

        Session.Editor!.State.Grid.CellSize.Should().Be(200);
    }

    [Fact]
    public async Task The_offset_is_limited_to_the_cell_size()
    {
        await OpenMapAsync();
        var cut = Render<MapControls>();
        ByLabel(cut, "Ajustes de cuadrícula").Click();

        Stepper(cut, "Desplazamiento X").Change("500");

        Session.Editor!.State.Grid.OffsetX.Should().Be(70);
    }

    [Fact]
    public async Task The_grid_switch_toggles_the_grid()
    {
        await OpenMapAsync();
        var cut = Render<MapControls>();

        GridSwitch(cut).Change(true);

        Session.Editor!.State.Grid.Enabled.Should().BeTrue();
    }

    [Fact]
    public async Task Read_only_hides_Draw_and_AddToken()
    {
        await OpenMapAsync(rawState: "esto no es json");
        Session.IsReadOnly.Should().BeTrue();

        var cut = Render<MapControls>();

        ButtonNames(cut).Should().NotContain(["Dibujar", "Añadir token"]);
    }

    [Fact]
    public async Task Read_only_disables_the_grid_switch()
    {
        await OpenMapAsync(rawState: "esto no es json");

        GridSwitch(Render<MapControls>()).HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public async Task Read_only_disables_the_grid_settings()
    {
        await OpenMapAsync(rawState: "esto no es json");

        ByLabel(Render<MapControls>(), "Ajustes de cuadrícula").HasAttribute("disabled").Should().BeTrue();
    }

    [Theory]
    [InlineData("Navegar")]
    [InlineData("Medir")]
    [InlineData("Ajustar a pantalla")]
    public async Task Read_only_keeps_navigate_measure_and_fit(string label)
    {
        await OpenMapAsync(rawState: "esto no es json");

        ButtonNames(Render<MapControls>()).Should().Contain(label);
    }

    [Fact]
    public async Task Read_only_ignores_a_remembered_Draw_mode_and_shows_Navigate_pressed()
    {
        await OpenMapAsync(rawState: "esto no es json");
        await Services.GetRequiredService<MapCanvasInterop>().SetModeAsync(MapMode.Draw, DrawTool.Default);

        var cut = Render<MapControls>();

        ByLabel(cut, "Navegar").GetAttribute("aria-pressed").Should().Be("true");
    }

    [Fact]
    public async Task AddToken_raises_the_callback()
    {
        await OpenMapAsync();
        var added = 0;
        var cut = Render<MapControls>(p => p.Add(x => x.OnAddToken, () => added++));

        ButtonText(cut, "Añadir token").Click();

        added.Should().Be(1);
    }

    [Fact]
    public async Task Fit_calls_the_canvas()
    {
        await OpenMapAsync();
        await Services.GetRequiredService<MapCanvasInterop>().LoadAsync(default);
        var cut = Render<MapControls>();

        ButtonText(cut, "Ajustar a pantalla").Click();

        JSInterop.Invocations.Should().Contain(i => i.Identifier == "mapCanvas.fit");
    }

    [Fact]
    public async Task Start_combat_button_shows_with_an_editable_map_and_no_combat()
    {
        await OpenMapAsync();

        var cut = Render<MapControls>();

        ButtonNames(cut).Should().Contain("Empezar combate");
    }

    [Fact]
    public async Task Start_combat_button_is_hidden_during_combat()
    {
        await OpenMapAsync();
        Services.GetRequiredService<CombatController>().Start([new InitiativeEntry("t1", 10)]);

        var cut = Render<MapControls>();

        ButtonNames(cut).Should().NotContain("Empezar combate");
    }

    [Fact]
    public async Task Start_combat_button_is_hidden_in_read_only()
    {
        await OpenMapAsync(rawState: "esto no es json");

        var cut = Render<MapControls>();

        ButtonNames(cut).Should().NotContain("Empezar combate");
    }

    [Fact]
    public async Task Start_combat_button_opens_the_dialog()
    {
        await OpenMapAsync();
        var cut = Render<MapControls>();

        ButtonText(cut, "Empezar combate").Click();

        Services.GetRequiredService<ShellState>().StartCombatOpen.Should().BeTrue();
    }

    private const string HideLabel = "Ocultar la barra de herramientas";
    private const string ShowLabel = "Mostrar la barra de herramientas";

    [Fact]
    public async Task Toggle_hides_and_shows_the_toolbar()
    {
        await OpenMapAsync();
        var cut = Render<MapControls>();

        ByLabel(cut, HideLabel).Click();
        var hidden = cut.FindAll("[aria-label='Modo']").Count;
        ByLabel(cut, ShowLabel).Click();

        (hidden, cut.FindAll("[aria-label='Modo']").Count).Should().Be((0, 1));
    }

    [Fact]
    public async Task Toggle_lives_inside_the_toolbar_as_its_last_group()
    {
        await OpenMapAsync();
        var cut = Render<MapControls>();

        cut.Find("[aria-label='Herramientas del mapa'] > .map-controls__toggle-group .map-controls__toggle").GetAttribute("aria-label")
            .Should().Be(HideLabel);
    }

    [Fact]
    public async Task Folded_toolbar_keeps_only_the_toggle()
    {
        await OpenMapAsync();
        var cut = Render<MapControls>();

        ByLabel(cut, HideLabel).Click();

        cut.FindAll("[aria-label='Herramientas del mapa'] button").Select(b => b.GetAttribute("aria-label"))
            .Should().Equal(ShowLabel);
    }

    [Fact]
    public async Task Hidden_toolbar_closes_grid_settings()
    {
        await OpenMapAsync();
        var cut = Render<MapControls>();
        ByLabel(cut, "Ajustes de cuadrícula").Click();

        ByLabel(cut, HideLabel).Click();
        ByLabel(cut, ShowLabel).Click();

        cut.FindAll("[aria-label='Ajustes de la cuadrícula']").Should().BeEmpty();
    }

    [Fact]
    public async Task Toolbar_visibility_is_remembered_in_preferences()
    {
        await OpenMapAsync();
        var cut = Render<MapControls>();

        ByLabel(cut, HideLabel).Click();

        Services.GetRequiredService<IAppPreferences>().Get("map.toolbar.visible").Should().Be("false");
    }

    [Fact]
    public async Task Toolbar_starts_hidden_when_preferences_say_so()
    {
        Services.GetRequiredService<IAppPreferences>().Set("map.toolbar.visible", "false");
        await OpenMapAsync();

        var cut = Render<MapControls>();

        cut.FindAll("[aria-label='Modo']").Should().BeEmpty();
    }

    [Fact]
    public async Task Mode_is_kept_when_hiding_the_toolbar()
    {
        await OpenMapAsync();
        await Services.GetRequiredService<MapCanvasInterop>().LoadAsync(default);
        var cut = Render<MapControls>();
        ButtonText(cut, "Medir").Click();

        ByLabel(cut, HideLabel).Click();
        ByLabel(cut, ShowLabel).Click();

        (Services.GetRequiredService<MapCanvasInterop>().Mode, ButtonText(cut, "Medir").GetAttribute("aria-pressed"))
            .Should().Be((MapMode.Measure, "true"));
    }
}
