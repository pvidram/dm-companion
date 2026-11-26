using Bunit;
using DmCompanion.Core.Vault.Storage;
using DmCompanion.Screens.Components;
using DmCompanion.Screens.Platform;
using DmCompanion.Screens.Services;
using DmCompanion.Screens.Tests.Support;
using DmCompanion.UI;
using DmCompanion.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DmCompanion.Screens.Tests;

public sealed class VaultCreationFlowTests : BunitContext, IAsyncLifetime
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "dmc-tests", Guid.NewGuid().ToString("N"));
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(10);
    private ShellState Shell => Services.GetRequiredService<ShellState>();

    public VaultCreationFlowTests()
    {
        Directory.CreateDirectory(_root);
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddDmCompanionUI();
        Services.AddSingleton<IAppPreferences>(new InMemoryAppPreferences());
        Services.AddSingleton<IVaultServiceFactory>(new FileSystemVaultServiceFactory(NullLoggerFactory.Instance));
        Services.AddSingleton<VaultHost>();
        Services.AddScoped<ShellState>();
        Services.AddVaultBrowsingForTests();
        Services.AddSingleton<IStorageAccess>(new TempStorageAccess(_root));
    }

    Task IAsyncLifetime.InitializeAsync() => Task.CompletedTask;

    async Task IAsyncLifetime.DisposeAsync()
    {
        await Services.GetRequiredService<VaultHost>().DisposeAsync();
        await base.DisposeAsync();
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    [Fact]
    public void Picker_shows_the_New_folder_button() =>
        Render<VaultPicker>().FindAll("button").Should().Contain(b => b.TextContent.Trim() == "Nueva carpeta");

    [Fact]
    public async Task New_folder_dialog_keeps_Create_disabled_while_the_name_is_empty()
    {
        var cut = await OpenDialogAsync();

        ButtonIn(cut, "Crear").HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public async Task Creating_a_folder_creates_it_and_the_browser_enters_it()
    {
        var cut = await OpenDialogAsync();

        await cut.InvokeAsync(() => Dialog(cut).QuerySelector("input")!.Input("Campañas"));
        await cut.InvokeAsync(() => ButtonIn(cut, "Crear").Click());

        var expected = Path.Combine(_root, "Campañas");
        cut.WaitForAssertion(() => (Directory.Exists(expected), cut.Find("p").TextContent).Should().Be((true, expected)));
    }

    [Fact]
    public async Task Creating_a_folder_with_an_invalid_name_shows_an_inline_error()
    {
        var cut = await OpenDialogAsync();

        await cut.InvokeAsync(() => Dialog(cut).QuerySelector("input")!.Input("a/b"));
        await cut.InvokeAsync(() => ButtonIn(cut, "Crear").Click());

        cut.WaitForAssertion(() => (Dialog(cut).QuerySelector("[role=alert]") is not null, Shell.NewFolder is not null,
            Directory.GetDirectories(_root).Length).Should().Be((true, true, 0)));
    }

    [Fact]
    public async Task Creating_a_folder_that_already_exists_says_so()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Campañas"));
        var cut = await OpenDialogAsync();

        await cut.InvokeAsync(() => Dialog(cut).QuerySelector("input")!.Input("campañas"));
        await cut.InvokeAsync(() => ButtonIn(cut, "Crear").Click());

        cut.WaitForAssertion(() => Dialog(cut).QuerySelector("[role=alert]")!.TextContent.Should().Contain("Ya existe"));
    }

    [Fact]
    public async Task Cancelling_the_new_folder_dialog_closes_it()
    {
        var cut = await OpenDialogAsync();

        await cut.InvokeAsync(() => ButtonIn(cut, "Cancelar").Click());

        Shell.NewFolder.Should().BeNull();
    }

    private async Task<IRenderedComponent<VaultPicker>> OpenDialogAsync()
    {
        var cut = Render<VaultPicker>();
        await cut.InvokeAsync(() => cut.FindAll("button").First(b => b.TextContent.Trim() == "Nueva carpeta").Click());
        cut.WaitForAssertion(() => Shell.NewFolder.Should().NotBeNull());
        return cut;
    }

    private static AngleSharp.Dom.IElement Dialog(IRenderedComponent<VaultPicker> cut) =>
        cut.FindAll(".ds-dialog").First(d => d.TextContent.Contains("Nueva carpeta"));

    private static AngleSharp.Dom.IElement ButtonIn(IRenderedComponent<VaultPicker> cut, string text) =>
        Dialog(cut).QuerySelectorAll("button").First(b => b.TextContent.Trim() == text);

    [Fact]
    public async Task Using_an_empty_folder_offers_to_create_a_vault()
    {
        var cut = await UseCurrentAsync();

        var text = OpenDialogText(cut);
        (text.Contains("Crear vault aquí"), text.Contains("Abrir de todos modos"), text.Contains("Cancelar")).Should().Be((true, true, true));
    }

    [Fact]
    public async Task Using_one_of_the_storage_roots_does_not_offer_to_create()
    {
        ReplaceStorage(new TempStorageAccess(_root, "/", [_root]));

        var cut = await UseCurrentAsync();

        (OpenDialogText(cut).Contains("Abrir de todos modos"), OpenDialogText(cut).Contains("Crear vault aquí")).Should().Be((true, false));
    }

    [Fact]
    public async Task Using_a_platform_root_does_not_offer_to_create()
    {
        ReplaceStorage(new TempStorageAccess(_root, _root));

        var cut = await UseCurrentAsync();

        (OpenDialogText(cut).Contains("Abrir de todos modos"), OpenDialogText(cut).Contains("Crear vault aquí")).Should().Be((true, false));
    }

    [Fact]
    public async Task Using_a_platform_root_with_different_casing_does_not_offer_to_create()
    {
        ReplaceStorage(new TempStorageAccess(_root, _root.ToUpperInvariant()));

        var cut = await UseCurrentAsync();

        (OpenDialogText(cut).Contains("Abrir de todos modos"), OpenDialogText(cut).Contains("Crear vault aquí")).Should().Be((true, false));
    }

    [Fact]
    public async Task Using_a_folder_with_notes_opens_it_without_asking()
    {
        File.WriteAllText(Path.Combine(_root, "a.md"), "# a");

        var cut = await UseCurrentAsync();

        cut.WaitForAssertion(() => (Host.Current is not null, cut.FindAll(".ds-dialog-backdrop:not([hidden])").Count).Should().Be((true, 0)));
    }

    [Fact]
    public async Task Creating_the_vault_opens_it_with_the_default_system()
    {
        var cut = await OpenCreateDialogAsync();

        (CreateDialog(cut).QuerySelector("input")!.GetAttribute("value")).Should().Be(Path.GetFileName(_root));
        await cut.InvokeAsync(() => CreateButton(cut, "Crear").Click());

        cut.WaitForAssertion(() => Host.Current.Should().NotBeNull(), Wait);
        Host.Current!.RootPath.Should().Be(_root);
        File.Exists(Path.Combine(_root, ".obsidian", "app.json")).Should().BeTrue();
        NoteText().Should().Contain("system: dnd5e");
    }

    [Fact]
    public async Task Creating_the_vault_with_cosmere_writes_that_system()
    {
        var cut = await OpenCreateDialogAsync();

        await cut.InvokeAsync(() => CreateDialog(cut).QuerySelector("select")!.Change("1"));
        await cut.InvokeAsync(() => CreateButton(cut, "Crear").Click());

        cut.WaitForAssertion(() => Host.Current.Should().NotBeNull(), Wait);
        NoteText().Should().Contain("system: cosmere");
    }

    [Fact]
    public async Task Create_is_disabled_without_a_campaign_name()
    {
        var cut = await OpenCreateDialogAsync();

        await cut.InvokeAsync(() => CreateDialog(cut).QuerySelector("input")!.Input(""));

        CreateButton(cut, "Crear").HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public async Task Creating_over_an_existing_note_shows_the_error_and_opens_nothing()
    {
        var existing = Path.Combine(_root, Path.GetFileName(_root) + ".md");
        var cut = await OpenCreateDialogAsync();
        File.WriteAllText(existing, "previo");

        await cut.InvokeAsync(() => CreateButton(cut, "Crear").Click());

        cut.WaitForAssertion(() => CreateDialog(cut).QuerySelector("[role=alert]")!.TextContent.Should().Contain("Ya existe"));
        (Host.Current, File.ReadAllText(existing)).Should().Be((null, "previo"));
    }

    [Fact]
    public async Task Creating_twice_does_not_write_twice()
    {
        var cut = await OpenCreateDialogAsync();
        await cut.InvokeAsync(() => CreateButton(cut, "Crear").Click());
        cut.WaitForAssertion(() => Host.Current.Should().NotBeNull(), Wait);

        var again = await Services.GetRequiredService<IVaultCreator>().CreateAsync(_root, Path.GetFileName(_root), "dnd5e");

        again.Status.Should().Be(CreateVaultStatus.NoteExists);
    }

    private VaultHost Host => Services.GetRequiredService<VaultHost>();

    private void ReplaceStorage(IStorageAccess storage) => Services.AddSingleton(storage);

    private string NoteText() => File.ReadAllText(Directory.GetFiles(_root, "*.md").Single());

    private async Task<IRenderedComponent<VaultPicker>> UseCurrentAsync()
    {
        var cut = Render<VaultPicker>();
        await cut.InvokeAsync(() => cut.FindAll("button").First(b => b.TextContent.Trim() == "Usar esta carpeta").Click());
        return cut;
    }

    private async Task<IRenderedComponent<VaultPicker>> OpenCreateDialogAsync()
    {
        var cut = await UseCurrentAsync();
        await cut.InvokeAsync(() => cut.FindAll(".ds-dialog button").First(b => b.TextContent.Trim() == "Crear vault aquí").Click());
        cut.WaitForAssertion(() => Shell.CreateVault.Should().NotBeNull());
        return cut;
    }

    private static string OpenDialogText(IRenderedComponent<VaultPicker> cut) =>
        string.Concat(cut.FindAll(".ds-dialog-backdrop:not([hidden])").Select(d => d.TextContent));

    private static AngleSharp.Dom.IElement CreateDialog(IRenderedComponent<VaultPicker> cut) =>
        cut.FindAll(".ds-dialog").First(d => d.TextContent.Contains("Crear vault de campaña"));

    private static AngleSharp.Dom.IElement CreateButton(IRenderedComponent<VaultPicker> cut, string text) =>
        CreateDialog(cut).QuerySelectorAll("button").First(b => b.TextContent.Trim() == text);

    private sealed class TempStorageAccess(string root, string browseRoot = "/", IReadOnlyList<string>? roots = null) : IStorageAccess
    {
        public bool HasAllFilesAccess => true;
        public string DefaultBrowseRoot => browseRoot;
        public void RequestAllFilesAccess() { }
        public string? InitialPath => root;
        public IReadOnlyList<string> Roots => roots ?? [];
    }
}
