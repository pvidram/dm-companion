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

public sealed class NoteEditorTests : BunitContext, IAsyncLifetime
{
    private const string Lyra = "Personajes/Lyra.md";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly TempVault _vault = TempVault.FromDemo();

    public NoteEditorTests()
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
        Services.AddSingleton(TimeProvider.System);
        Services.AddSingleton<MapSession>();
        Services.AddSingleton<TokenHitPoints>();
        Services.AddSingleton<CombatController>();
        Services.AddSingleton<MapLibrary>();
        Services.AddScoped<MapCanvasInterop>();
        Services.AddScoped<TurnStripInterop>();
        Services.AddScoped<DragPanelInterop>();
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
    private VaultHost Host => Services.GetRequiredService<VaultHost>();

    private IRenderedComponent<DmShell> RenderEditing(string path = Lyra)
    {
        var cut = Render<DmShell>();
        cut.InvokeAsync(() => Shell.EditNote(path)).GetAwaiter().GetResult();
        cut.WaitForAssertion(() => cut.Find("textarea"), Timeout);
        return cut;
    }

    // Input solo devuelve cuando se ha renderizado, pero el estado compartido lo publica el manejador: se espera a que conste.
    private void TypeAndWaitDirty(IRenderedComponent<DmShell> cut, string text)
    {
        cut.Find("textarea").Input(text);
        cut.WaitForAssertion(() => Shell.EditorDirty.Should().BeTrue(), Timeout);
    }

    private static AngleSharp.Dom.IElement Button(IRenderedComponent<DmShell> cut, string text) =>
        cut.FindAll("button").First(b => b.TextContent.Trim() == text);

    private static AngleSharp.Dom.IElement DialogButton(IRenderedComponent<DmShell> cut, string dialogTitle, string text) =>
        cut.FindAll(".ds-dialog").First(d => d.TextContent.Contains(dialogTitle)).QuerySelectorAll("button").First(b => b.TextContent.Trim() == text);

    private static bool IsOpen(AngleSharp.Dom.IElement dialog) => !dialog.ParentElement!.HasAttribute("hidden");

    [Fact]
    public void Opens_with_the_raw_markdown_of_the_note()
    {
        var cut = RenderEditing();

        cut.Find("textarea").GetAttribute("value").Should().Contain("name: Lyra Vientoalto");
    }

    [Fact]
    public void Saving_updates_the_title_in_the_index_and_shows_a_notice()
    {
        var cut = RenderEditing();
        var edited = File.ReadAllText(_vault.PathOf(Lyra)).Replace("Lyra Vientoalto", "Lyra Nueva");

        TypeAndWaitDirty(cut, edited);
        Button(cut, "Guardar").Click();

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Guardado"), Timeout);
        Host.Current!.Index.Get(Lyra)!.Title.Should().Be("Lyra Nueva");
        File.ReadAllText(_vault.PathOf(Lyra)).Should().Contain("Lyra Nueva");
    }

    [Fact]
    public void External_change_then_save_opens_the_conflict_dialog_and_overwrite_writes()
    {
        var cut = RenderEditing();
        TouchExternally("externo");
        TypeAndWaitDirty(cut, "mi version");

        Button(cut, "Guardar").Click();

        cut.WaitForAssertion(() => IsOpen(cut.FindAll(".ds-dialog").First(d => d.TextContent.Contains("cambiado en disco"))).Should().BeTrue(), Timeout);
        File.ReadAllText(_vault.PathOf(Lyra)).Should().Be("externo");

        Button(cut, "Sobrescribir con mi versión").Click();

        cut.WaitForAssertion(() => File.ReadAllText(_vault.PathOf(Lyra)).Should().Be("mi version"), Timeout);
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Guardado"), Timeout);
        Shell.ActiveEditorDialog.Should().Be(EditorDialog.None);
    }

    [Fact]
    public void Discard_and_reload_shows_the_disk_content()
    {
        var cut = RenderEditing();
        TouchExternally("externo");
        TypeAndWaitDirty(cut, "mi version");
        Button(cut, "Guardar").Click();
        cut.WaitForAssertion(() => Shell.ActiveEditorDialog.Should().Be(EditorDialog.Conflict), Timeout);

        Button(cut, "Descartar y recargar").Click();

        cut.WaitForAssertion(() => cut.Find("textarea").GetAttribute("value").Should().Be("externo"), Timeout);
        File.ReadAllText(_vault.PathOf(Lyra)).Should().Be("externo");
    }

    [Fact]
    public void Keep_editing_closes_the_conflict_dialog_and_keeps_the_text()
    {
        var cut = RenderEditing();
        TouchExternally("externo");
        TypeAndWaitDirty(cut, "mi version");
        Button(cut, "Guardar").Click();
        cut.WaitForAssertion(() => Shell.ActiveEditorDialog.Should().Be(EditorDialog.Conflict), Timeout);

        DialogButton(cut, "Cerrar sin guardar", "Seguir editando").Click();

        cut.WaitForAssertion(() => (Shell.ActiveEditorDialog, cut.Find("textarea").GetAttribute("value")).Should().Be((EditorDialog.None, "mi version")), Timeout);
    }

    [Fact]
    public void Closing_with_unsaved_changes_asks_for_confirmation()
    {
        var cut = RenderEditing();
        TypeAndWaitDirty(cut, "cambiado");

        Button(cut, "Cerrar").Click();

        cut.WaitForAssertion(() => (Shell.ActiveEditorDialog, Shell.EditingPath).Should().Be((EditorDialog.ConfirmClose, Lyra)), Timeout);
        DialogButton(cut, "cambiado en disco", "Seguir editando").Click();
        cut.WaitForAssertion(() => Shell.EditingPath.Should().Be(Lyra), Timeout);

        Button(cut, "Cerrar").Click();
        cut.WaitForAssertion(() => Shell.ActiveEditorDialog.Should().Be(EditorDialog.ConfirmClose), Timeout);
        DialogButton(cut, "Cerrar sin guardar", "Cerrar sin guardar").Click();
        cut.WaitForAssertion(() => Shell.EditingPath.Should().BeNull(), Timeout);
    }

    [Fact]
    public void Closing_without_changes_closes_directly()
    {
        var cut = RenderEditing();

        Button(cut, "Cerrar").Click();

        cut.WaitForAssertion(() => (Shell.EditingPath, Shell.ActiveEditorDialog).Should().Be((null, EditorDialog.None)), Timeout);
    }

    [Fact]
    public async Task A_parent_rerender_keeps_the_typed_text()
    {
        var cut = RenderEditing();
        TypeAndWaitDirty(cut, "a medio escribir");

        await cut.InvokeAsync(() => Shell.Toggle(OverlayPanel.Notes));
        await cut.InvokeAsync(() => Shell.Toggle(OverlayPanel.Notes));
        cut.Render();

        cut.WaitForAssertion(() => cut.Find("textarea").GetAttribute("value").Should().Be("a medio escribir"), Timeout);
    }

    [Fact]
    public void Preview_renders_the_note_content_only_when_toggled()
    {
        var cut = RenderEditing();
        TypeAndWaitDirty(cut, "---\ntype: note\n---\nHola **mundo**");
        cut.FindAll(".ds-note").Should().BeEmpty();

        Button(cut, "Vista previa").Click();

        cut.WaitForAssertion(() => cut.Find(".ds-note strong").TextContent.Should().Be("mundo"), Timeout);
        cut.FindAll("textarea").Should().BeEmpty();

        Button(cut, "Editar").Click();
        cut.WaitForAssertion(() => cut.Find("textarea").GetAttribute("value").Should().Contain("Hola **mundo**"), Timeout);
    }

    private void TouchExternally(string content)
    {
        File.WriteAllText(_vault.PathOf(Lyra), content);
        File.SetLastWriteTimeUtc(_vault.PathOf(Lyra), DateTime.UtcNow.AddMinutes(1));
    }

    // --- Texto sin guardar al navegar o cambiar de vault ---

    private const string Varek = "Personajes/Varek.md";

    private void SelectInTree(IRenderedComponent<DmShell> cut, string name) =>
        cut.FindAll(".ds-list-row").First(r => r.TextContent.Contains(name)).Click();

    private void ExpandAndSelectVarek(IRenderedComponent<DmShell> cut)
    {
        cut.Find("input[type=search]").Input("Varek");
        cut.WaitForAssertion(() => cut.FindAll("section.ds-parchment .ds-list-row").Should().NotBeEmpty(), Timeout);
        cut.FindAll("section.ds-parchment .ds-list-row").First(r => r.TextContent.Contains("Varek")).Click();
    }

    [Fact]
    public void Dirty_and_selecting_another_note_opens_the_confirmation()
    {
        var cut = RenderEditing();
        TypeAndWaitDirty(cut, "sin guardar");

        ExpandAndSelectVarek(cut);

        cut.WaitForAssertion(() => Shell.ActiveEditorDialog.Should().Be(EditorDialog.ConfirmClose), Timeout);
    }

    [Fact]
    public void Dirty_and_selecting_another_note_keeps_the_typed_text()
    {
        var cut = RenderEditing();
        TypeAndWaitDirty(cut, "sin guardar");

        ExpandAndSelectVarek(cut);

        cut.WaitForAssertion(() => Shell.ActiveEditorDialog.Should().Be(EditorDialog.ConfirmClose), Timeout);
        cut.Find("textarea").GetAttribute("value").Should().Be("sin guardar");
    }

    [Fact]
    public void Confirming_the_discard_opens_the_other_note()
    {
        var cut = RenderEditing();
        TypeAndWaitDirty(cut, "sin guardar");
        ExpandAndSelectVarek(cut);
        cut.WaitForAssertion(() => Shell.ActiveEditorDialog.Should().Be(EditorDialog.ConfirmClose), Timeout);

        DialogButton(cut, "Cerrar sin guardar", "Cerrar sin guardar").Click();

        cut.WaitForAssertion(() => (Shell.SelectedNotePath, Shell.EditingPath).Should().Be((Varek, null)), Timeout);
    }

    [Fact]
    public void Cancelling_the_discard_keeps_editing_the_same_note()
    {
        var cut = RenderEditing();
        TypeAndWaitDirty(cut, "sin guardar");
        ExpandAndSelectVarek(cut);
        cut.WaitForAssertion(() => Shell.ActiveEditorDialog.Should().Be(EditorDialog.ConfirmClose), Timeout);

        DialogButton(cut, "Cerrar sin guardar", "Seguir editando").Click();

        cut.WaitForAssertion(() => Shell.ActiveEditorDialog.Should().Be(EditorDialog.None), Timeout);
        (Shell.EditingPath, Shell.ActiveEditorDialog).Should().Be((Lyra, EditorDialog.None));
    }

    [Fact]
    public void Not_dirty_navigation_is_immediate()
    {
        var cut = RenderEditing();

        ExpandAndSelectVarek(cut);

        cut.WaitForAssertion(() => Shell.SelectedNotePath.Should().Be(Varek), Timeout);
        Shell.ActiveEditorDialog.Should().Be(EditorDialog.None);
    }

    [Fact]
    public void Dirty_and_editing_another_note_from_the_players_panel_asks_first()
    {
        var cut = RenderEditing();
        TypeAndWaitDirty(cut, "sin guardar");

        _ = cut.InvokeAsync(() => Shell.RequestEditNote(Varek));

        cut.WaitForAssertion(() => (Shell.EditingPath, Shell.ActiveEditorDialog).Should().Be((Lyra, EditorDialog.ConfirmClose)), Timeout);
    }

    [Fact]
    public async Task Dirty_and_editing_the_same_note_again_does_nothing()
    {
        var cut = RenderEditing();
        TypeAndWaitDirty(cut, "sin guardar");

        await cut.InvokeAsync(() => Shell.RequestEditNote(Lyra));

        (Shell.ActiveEditorDialog, Shell.EditorDirty, cut.Find("textarea").GetAttribute("value"))
            .Should().Be((EditorDialog.None, true, "sin guardar"));
        _ = cut.InvokeAsync(() => Shell.RequestEditNote(Varek));
        cut.WaitForAssertion(() => Shell.ActiveEditorDialog.Should().Be(EditorDialog.ConfirmClose), Timeout);
    }

    [Fact]
    public async Task Closing_the_editor_ignores_a_stale_pending_navigation()
    {
        var cut = RenderEditing();
        TypeAndWaitDirty(cut, "sin guardar");
        await cut.InvokeAsync(() => Shell.RequestEditNote(Varek));
        await cut.InvokeAsync(Shell.DismissEditorDialog); // el diálogo se cierra sin pasar por CancelDiscard: queda una petición vieja

        Button(cut, "Cerrar").Click();
        cut.WaitForAssertion(() => Shell.ActiveEditorDialog.Should().Be(EditorDialog.ConfirmClose), Timeout);
        DialogButton(cut, "Cerrar sin guardar", "Cerrar sin guardar").Click();

        cut.WaitForAssertion(() => (Shell.EditingPath, Shell.SelectedNotePath).Should().Be((null, Lyra)), Timeout);
    }

    [Fact]
    public void Dirty_and_change_vault_asks_first_and_keeps_the_vault_open()
    {
        var cut = RenderEditing();
        TypeAndWaitDirty(cut, "sin guardar");
        var current = Host.Current!.RootPath;
        using var other = TempVault.FromDemo();

        _ = cut.InvokeAsync(() => VaultBrowsing.SwitchAsync(Host, Shell, other.Root));

        cut.WaitForAssertion(() => (Shell.ActiveEditorDialog, Host.Current?.RootPath).Should().Be((EditorDialog.ConfirmClose, current)), Timeout);
    }

    [Fact]
    public async Task Confirming_the_discard_switches_the_vault()
    {
        var cut = RenderEditing();
        TypeAndWaitDirty(cut, "sin guardar");
        using var other = TempVault.FromDemo();
        _ = cut.InvokeAsync(() => VaultBrowsing.SwitchAsync(Host, Shell, other.Root));
        await cut.WaitForAssertionAsync(() => Shell.ActiveEditorDialog.Should().Be(EditorDialog.ConfirmClose), Timeout);

        DialogButton(cut, "Cerrar sin guardar", "Cerrar sin guardar").Click();

        await cut.WaitForAssertionAsync(() => Host.Current?.RootPath.Should().Be(other.Root), Timeout);
    }

    [Fact]
    public void Saving_clears_the_dirty_flag_so_navigation_is_immediate()
    {
        var cut = RenderEditing();
        TypeAndWaitDirty(cut, "guardado " + File.ReadAllText(_vault.PathOf(Lyra)));
        Button(cut, "Guardar").Click();
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Guardado"), Timeout);

        _ = cut.InvokeAsync(() => Shell.RequestOpenNote(Varek));

        cut.WaitForAssertion(() => Shell.EditingPath.Should().BeNull(), Timeout);
        Shell.ActiveEditorDialog.Should().Be(EditorDialog.None);
    }

    [Fact]
    public void Typing_clears_the_saved_notice()
    {
        var cut = RenderEditing();
        TypeAndWaitDirty(cut, "x");
        Button(cut, "Guardar").Click();
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Guardado"), Timeout);

        TypeAndWaitDirty(cut, "xy");

        cut.WaitForAssertion(() => cut.Markup.Should().NotContain("Guardado"), Timeout);
    }

    [Fact]
    public async Task Close_is_available_when_the_initial_load_failed()
    {
        var cut = Render<DmShell>();
        await cut.InvokeAsync(() => Shell.EditNote("Personajes/No existe.md"));

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("No se pudo leer"), Timeout);

        Button(cut, "Cerrar").Should().NotBeNull();
    }
}
