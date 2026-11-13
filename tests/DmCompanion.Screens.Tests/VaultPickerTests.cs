using Bunit;
using DmCompanion.Core.Vault.Storage;
using DmCompanion.Infrastructure;
using DmCompanion.Screens.Components;
using DmCompanion.Screens.Platform;
using DmCompanion.Screens.Services;
using DmCompanion.UI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DmCompanion.Screens.Tests;

public sealed class VaultPickerTests : BunitContext, IAsyncLifetime
{
    private readonly FakeStorageAccess _storage = new();
    private readonly FakeFolderBrowser _browser = new();

    public VaultPickerTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddDmCompanionUI();
        Services.AddSingleton<IAppPreferences>(new InMemoryAppPreferences());
        Services.AddSingleton<IStorageAccess>(_storage);
        Services.AddSingleton<IFolderBrowser>(_browser);
        Services.AddSingleton<IVaultServiceFactory>(new FileSystemVaultServiceFactory(NullLoggerFactory.Instance));
        Services.AddSingleton<VaultHost>();
        Services.AddScoped<ShellState>();
    }

    Task IAsyncLifetime.InitializeAsync() => Task.CompletedTask;

    async Task IAsyncLifetime.DisposeAsync()
    {
        await Services.GetRequiredService<VaultHost>().DisposeAsync();
        await base.DisposeAsync();
    }

    [Fact]
    public void Without_CanTypePath_there_is_no_paste_field() =>
        Render<VaultPicker>().Markup.Should().NotContain("Pegar una ruta");

    [Fact]
    public void Pasting_an_existing_path_navigates_to_it()
    {
        _storage.CanTypePath = true;
        _browser.Existing.Add("/datos/vault");
        var cut = Render<VaultPicker>();

        cut.Find("input").Input("/datos/vault");
        ButtonWith(cut, "Ir").Click();

        cut.Find("p").TextContent.Should().Be("/datos/vault");
    }

    [Fact]
    public void Pasting_a_missing_path_shows_a_notice()
    {
        _storage.CanTypePath = true;
        var cut = Render<VaultPicker>();

        cut.Find("input").Input("/no/existe");
        ButtonWith(cut, "Ir").Click();

        cut.Markup.Should().Contain("No existe la carpeta");
    }

    [Fact]
    public void Roots_are_shown_as_buttons_and_navigate()
    {
        _storage.CanTypePath = true;
        _storage.Roots = ["C:\\", "E:\\"];
        var cut = Render<VaultPicker>();

        ButtonWith(cut, "E:\\").Click();

        cut.Find("p").TextContent.Should().Be("E:\\");
    }

    [Fact]
    public void Roots_render_one_button_each()
    {
        _storage.CanTypePath = true;
        _storage.Roots = ["C:\\", "E:\\"];

        Render<VaultPicker>().FindAll("button").Count(b => b.TextContent.EndsWith(":\\")).Should().Be(2);
    }

    [Fact]
    public void InitialPath_is_where_the_picker_starts()
    {
        _storage.InitialPath = "/inicio";

        Render<VaultPicker>().Find("p").TextContent.Should().Be("/inicio");
    }

    [Fact]
    public void Going_up_from_a_windows_folder_reaches_the_drive_root()
    {
        _storage.CanTypePath = true;
        _storage.InitialPath = "C:\\Users";
        var cut = Render<VaultPicker>();

        ButtonWith(cut, "Subir").Click();

        cut.Find("p").TextContent.Should().Be("C:\\");
    }

    [Fact]
    public void Up_is_disabled_at_the_drive_root()
    {
        _storage.CanTypePath = true;
        _storage.InitialPath = "C:\\";

        ButtonWith(Render<VaultPicker>(), "Subir").HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public void Pasting_a_relative_path_shows_a_notice_and_does_not_navigate()
    {
        _storage.CanTypePath = true;
        _storage.InitialPath = "/inicio";
        _browser.Existing.Add("datos/vault");
        var cut = Render<VaultPicker>();

        cut.Find("input").Input("datos/vault");
        ButtonWith(cut, "Ir").Click();

        cut.Markup.Should().Contain("Escribe la ruta completa");
    }

    [Fact]
    public void Pasting_a_relative_path_keeps_the_current_folder()
    {
        _storage.CanTypePath = true;
        _storage.InitialPath = "/inicio";
        _browser.Existing.Add("datos/vault");
        var cut = Render<VaultPicker>();

        cut.Find("input").Input("datos/vault");
        ButtonWith(cut, "Ir").Click();

        cut.Find("p").TextContent.Should().Be("/inicio");
    }

    [Fact]
    public void Pasting_a_path_with_dot_segments_navigates_to_the_normalized_path()
    {
        _storage.CanTypePath = true;
        _browser.Existing.Add("/datos/vault");
        var cut = Render<VaultPicker>();

        cut.Find("input").Input("/datos/otra/../vault");
        ButtonWith(cut, "Ir").Click();

        cut.Find("p").TextContent.Should().Be("/datos/vault");
    }

    [Fact]
    public async Task Using_a_folder_that_looks_like_a_vault_opens_it_without_asking()
    {
        using var folder = TempVault.Empty();
        _storage.InitialPath = folder.Root;
        var cut = Render<VaultPicker>();

        ButtonWith(cut, "Usar esta carpeta").Click();

        await cut.WaitForAssertionAsync(() => Services.GetRequiredService<VaultHost>().Current.Should().NotBeNull());
    }

    [Fact]
    public void Using_a_folder_that_does_not_look_like_a_vault_asks_for_confirmation()
    {
        using var folder = TempVault.Empty();
        _storage.InitialPath = folder.Root;
        _browser.Unlikely.Add(folder.Root);
        var cut = Render<VaultPicker>();

        ButtonWith(cut, "Usar esta carpeta").Click();

        cut.Find(".ds-dialog-backdrop").HasAttribute("hidden").Should().BeFalse();
    }

    [Fact]
    public void The_confirmation_explains_that_all_the_content_is_indexed_and_watched()
    {
        using var folder = TempVault.Empty();
        _storage.InitialPath = folder.Root;
        _browser.Unlikely.Add(folder.Root);
        var cut = Render<VaultPicker>();

        ButtonWith(cut, "Usar esta carpeta").Click();

        cut.Find(".ds-dialog").TextContent.Should().Contain("se indexará y se vigilará todo su contenido");
    }

    [Fact]
    public void Asking_for_confirmation_does_not_open_the_folder_yet()
    {
        using var folder = TempVault.Empty();
        _storage.InitialPath = folder.Root;
        _browser.Unlikely.Add(folder.Root);
        var cut = Render<VaultPicker>();

        ButtonWith(cut, "Usar esta carpeta").Click();

        Services.GetRequiredService<VaultHost>().Current.Should().BeNull();
    }

    [Fact]
    public void Cancelling_the_confirmation_closes_the_dialog_without_opening()
    {
        using var folder = TempVault.Empty();
        _storage.InitialPath = folder.Root;
        _browser.Unlikely.Add(folder.Root);
        var cut = Render<VaultPicker>();
        ButtonWith(cut, "Usar esta carpeta").Click();

        ButtonWith(cut, "Cancelar").Click();

        cut.Find(".ds-dialog-backdrop").HasAttribute("hidden").Should().BeTrue();
    }

    [Fact]
    public async Task Confirming_opens_the_folder()
    {
        using var folder = TempVault.Empty();
        _storage.InitialPath = folder.Root;
        _browser.Unlikely.Add(folder.Root);
        var cut = Render<VaultPicker>();
        ButtonWith(cut, "Usar esta carpeta").Click();

        ButtonWith(cut, "Abrir de todos modos").Click();

        await cut.WaitForAssertionAsync(() => Services.GetRequiredService<VaultHost>().Current.Should().NotBeNull());
    }

    private static AngleSharp.Dom.IElement ButtonWith(IRenderedComponent<VaultPicker> cut, string text) =>
        cut.FindAll("button").First(b => b.TextContent.Trim() == text);

    private sealed class FakeStorageAccess : IStorageAccess
    {
        public bool HasAllFilesAccess => true;
        public string DefaultBrowseRoot { get; set; } = "/";
        public void RequestAllFilesAccess() { }
        public bool CanTypePath { get; set; }
        public IReadOnlyList<string> Roots { get; set; } = [];
        public string? InitialPath { get; set; }
    }

    private sealed class FakeFolderBrowser : IFolderBrowser
    {
        public HashSet<string> Existing { get; } = [];
        public HashSet<string> Unlikely { get; } = [];
        public bool LooksLikeVault(string path) => !Unlikely.Contains(path);
        public IReadOnlyList<string> ListSubdirectories(string path) => [];
        public bool DirectoryExists(string path) => Existing.Contains(path);
    }
}
