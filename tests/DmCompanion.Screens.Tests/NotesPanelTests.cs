using DmCompanion.Screens.Tests.Support;
using Bunit;
using DmCompanion.Core.Vault;
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

public sealed class NotesPanelTests : BunitContext, IAsyncLifetime
{
    private const string Lyra = "Personajes/Lyra.md";
    private const string Varek = "Personajes/Varek.md";
    private const string FaroViejo = "Lugares/Faro Viejo.md";
    private const string NotasRotas = "Historia/Notas rotas.md";

    private readonly TempVault _vault = TempVault.FromDemo();

    public NotesPanelTests()
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
        Services.AddGlossaryForTests();
        Services.AddSoundForTests();
        Services.AddSingleton(TimeProvider.System);
        Services.AddSingleton<MapSession>();
        Services.AddSingleton<TokenHitPoints>();
        Services.AddSingleton<CombatController>();
        Services.AddSingleton<MapLibrary>();
        Services.AddScoped<MapCanvasInterop>();
        Services.AddScoped<TurnStripInterop>();
        Services.AddWindowingForTests();
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

    private IRenderedComponent<DmShell> RenderOpenNotes(string? selected = null)
    {
        var cut = Render<DmShell>();
        cut.InvokeAsync(() => { if (selected is null) Shell.Toggle(OverlayPanel.Notes); else Shell.OpenNote(selected); }).GetAwaiter().GetResult();
        cut.WaitForState(() => !cut.Find("section.ds-parchment").HasAttribute("hidden"));
        return cut;
    }

    private static AngleSharp.Dom.IElement Dialog(IRenderedComponent<DmShell> cut) => cut.Find(".ds-dialog");

    [Fact]
    public void Searching_body_text_lists_Lyra()
    {
        var cut = RenderOpenNotes();

        cut.Find("input[type=search]").Input("hermano");

        cut.WaitForAssertion(() => cut.Find("section.ds-parchment").TextContent.Should().Contain("Lyra"));
    }

    [Fact]
    public void Selecting_a_search_result_opens_the_note()
    {
        var cut = RenderOpenNotes();
        cut.Find("input[type=search]").Input("hermano");
        cut.WaitForAssertion(() => cut.FindAll("button.ds-list-row").Should().Contain(b => b.TextContent.Contains("Lyra")), TimeSpan.FromSeconds(10));

        cut.FindAll("button.ds-list-row").First(b => b.TextContent.Contains("Lyra")).Click();

        cut.WaitForAssertion(() => Shell.SelectedNotePath.Should().Be(Lyra), TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void Tree_does_not_show_the_hidden_obsidian_folder()
    {
        var cut = RenderOpenNotes();

        var rows = cut.FindAll("ul.ds-tree button.ds-list-row").Select(b => b.TextContent.Trim()).ToList();

        rows.Should().BeEquivalentTo(["Historia", "Lugares", "Mapas", "Personajes", "Campaña"]);
    }

    [Fact]
    public void Tree_leaf_opens_the_note()
    {
        var cut = RenderOpenNotes();
        cut.FindAll("ul.ds-tree button.ds-list-row").First(b => b.TextContent.Trim() == "Personajes").Click();
        cut.WaitForAssertion(() => cut.FindAll("ul.ds-tree button.ds-list-row").Should().Contain(b => b.TextContent.Trim() == "Lyra"), TimeSpan.FromSeconds(10));

        cut.FindAll("ul.ds-tree button.ds-list-row").First(b => b.TextContent.Trim() == "Lyra").Click();

        cut.WaitForAssertion(() => Shell.SelectedNotePath.Should().Be(Lyra), TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void Reader_shows_the_title_and_the_rendered_body()
    {
        var cut = RenderOpenNotes(Lyra);

        cut.Find("section.ds-parchment article.ds-note").Should().NotBeNull();
        cut.Find("[data-testid=note-title]").TextContent.Should().Contain("Lyra");
    }

    [Fact]
    public void Reader_marks_secret_notes()
    {
        var cut = RenderOpenNotes("Personajes/Capitana Ysolde.md");

        cut.Find("section.ds-parchment").TextContent.Should().Contain("secreta");
    }

    [Fact]
    public void Reader_of_a_broken_note_shows_a_warning_notice()
    {
        var cut = RenderOpenNotes(NotasRotas);

        cut.Find("section.ds-parchment .ds-notice--warning").Should().NotBeNull();
    }

    [Fact]
    public void Reader_lists_backlinks_and_following_one_opens_that_note()
    {
        var cut = RenderOpenNotes("Lugares/Puerto Sal.md");
        var backlinks = cut.Find("[data-testid=backlinks]");

        backlinks.QuerySelectorAll("button.ds-list-row").Should().NotBeEmpty();
        backlinks.QuerySelector("button.ds-list-row")!.Click();

        cut.WaitForAssertion(() => Shell.SelectedNotePath.Should().NotBe("Lugares/Puerto Sal.md"), TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void Reader_attaches_wikilink_handling_to_the_content()
    {
        RenderOpenNotes(Lyra);

        JSInterop.Invocations["dmWikilinks.attach"].Should().NotBeEmpty();
    }

    [Fact]
    public async Task Clicking_an_existing_wikilink_selects_that_note()
    {
        var cut = RenderOpenNotes(Lyra);

        await cut.InvokeAsync(() => cut.FindComponent<NoteBody>().Instance.OnWikilinkClicked("Varek", false));

        Shell.SelectedNotePath.Should().Be(Varek);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task A_wikilink_click_without_target_is_a_no_op(string? target)
    {
        var cut = RenderOpenNotes(Lyra);

        await cut.InvokeAsync(() => cut.FindComponent<NoteBody>().Instance.OnWikilinkClicked(target!, false));

        (Shell.NewNote, Shell.SelectedNotePath).Should().Be((null, Lyra));
    }

    [Fact]
    public async Task Clicking_a_broken_wikilink_opens_the_new_note_dialog_with_title_and_folder()
    {
        var cut = RenderOpenNotes(FaroViejo);

        await cut.InvokeAsync(() => cut.FindComponent<NoteBody>().Instance.OnWikilinkClicked("Mazmorra del Kraken", true));

        cut.WaitForAssertion(() =>
        {
            Dialog(cut).ParentElement!.HasAttribute("hidden").Should().BeFalse();
            cut.Find(".ds-dialog input[type=text]").GetAttribute("value").Should().Be("Mazmorra del Kraken");
            cut.FindAll(".ds-dialog input[type=text]")[1].GetAttribute("value").Should().Be("Lugares");
        });
    }

    [Fact]
    public void New_note_dialog_lives_in_the_dialogs_slot_not_in_a_parchment()
    {
        var cut = RenderOpenNotes();

        cut.FindAll("section.ds-parchment .ds-dialog").Should().BeEmpty();
        cut.FindAll(".ds-dialog").Should().HaveCount(12); // nueva nota, añadir jugador, nombre de mapa, borrar mapa, descartar mapa, conflictos de sincronización, conflicto de escritura, cerrar sin guardar, añadir token, empezar combate, terminar combate y abrir una carpeta que no parece un vault
    }

    [Fact]
    public async Task Creating_a_note_with_an_existing_title_shows_AlreadyExists()
    {
        var cut = RenderOpenNotes();
        await cut.InvokeAsync(() => Shell.RequestNewNote("Varek", "Personajes"));
        cut.WaitForAssertion(() => Dialog(cut).ParentElement!.HasAttribute("hidden").Should().BeFalse());

        cut.FindAll(".ds-dialog button").First(b => b.TextContent.Contains("Crear")).Click();

        cut.WaitForAssertion(() => cut.Find(".ds-dialog .ds-notice").TextContent.Should().Contain("Ya existe"));
        Shell.NewNote.Should().NotBeNull();
    }

    [Fact]
    public async Task Creating_a_new_note_closes_the_dialog_and_opens_the_editor()
    {
        var cut = RenderOpenNotes();
        await cut.InvokeAsync(() => Shell.RequestNewNote("Mazmorra del Kraken", "Lugares"));
        cut.WaitForAssertion(() => Dialog(cut).ParentElement!.HasAttribute("hidden").Should().BeFalse());

        cut.FindAll(".ds-dialog button").First(b => b.TextContent.Contains("Crear")).Click();

        cut.WaitForAssertion(() => Shell.EditingPath.Should().Be("Lugares/Mazmorra del Kraken.md"));
        Shell.NewNote.Should().BeNull();
        File.Exists(_vault.PathOf("Lugares/Mazmorra del Kraken.md")).Should().BeTrue();
    }

    [Fact]
    public async Task Creating_a_note_with_an_invalid_folder_shows_an_error_notice()
    {
        var cut = RenderOpenNotes();
        await cut.InvokeAsync(() => Shell.RequestNewNote("Fuga", "../fuera"));
        cut.WaitForAssertion(() => Dialog(cut).ParentElement!.HasAttribute("hidden").Should().BeFalse());

        cut.FindAll(".ds-dialog button").First(b => b.TextContent.Contains("Crear")).Click();

        cut.WaitForAssertion(() => cut.Find(".ds-dialog .ds-notice--error").Should().NotBeNull());
        Shell.NewNote.Should().NotBeNull();
    }

    [Fact]
    public async Task Cancel_closes_the_new_note_dialog()
    {
        var cut = RenderOpenNotes();
        await cut.InvokeAsync(() => Shell.RequestNewNote("x", null));
        cut.WaitForAssertion(() => Dialog(cut).ParentElement!.HasAttribute("hidden").Should().BeFalse());

        cut.FindAll(".ds-dialog button").First(b => b.TextContent.Contains("Cancelar")).Click();

        cut.WaitForAssertion(() => Shell.NewNote.Should().BeNull(), TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void Escape_on_the_open_notes_parchment_closes_it()
    {
        var cut = RenderOpenNotes();

        cut.Find("section.ds-parchment").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        cut.WaitForAssertion(() =>
        {
            Shell.ActivePanel.Should().Be(OverlayPanel.None);
            cut.Find("section.ds-parchment").HasAttribute("hidden").Should().BeTrue();
        });
    }

    [Fact]
    public async Task Change_vault_resets_the_shell()
    {
        var cut = RenderOpenNotes(Lyra);
        await cut.InvokeAsync(() => Shell.RequestNewNote("x", null));
        cut.WaitForAssertion(() => Dialog(cut).ParentElement!.HasAttribute("hidden").Should().BeFalse(), TimeSpan.FromSeconds(10));

        using var other = TempVault.FromDemo();

        await cut.InvokeAsync(() => VaultBrowsing.SwitchAsync(Services.GetRequiredService<VaultHost>(), Shell, other.Root));

        await cut.WaitForAssertionAsync(() =>
            (Shell.ActivePanel, Shell.SelectedNotePath, Shell.NewNote).Should().Be((OverlayPanel.None, null, null)));
    }
}
