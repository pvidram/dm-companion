using DmCompanion.Core.Maps;
using DmCompanion.Core.Vault;
using DmCompanion.Infrastructure;
using DmCompanion.Screens.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace DmCompanion.Screens.Tests;

public sealed class MapLibraryTests : IAsyncLifetime
{
    private readonly TempVault _vault = TempVault.Empty();
    private readonly InMemoryAppPreferences _prefs = new();
    private readonly VaultHost _host;
    private readonly MapSession _session;
    private readonly MapLibrary _library;

    public MapLibraryTests()
    {
        _host = new VaultHost(new FileSystemVaultServiceFactory(NullLoggerFactory.Instance), _prefs, NullLogger<VaultHost>.Instance);
        _session = new MapSession(_host, _prefs, TimeProvider.System, NullLogger<MapSession>.Instance);
        _library = new MapLibrary(_host, _session);
    }

    public Task InitializeAsync() => _host.OpenAsync(_vault.Root);

    public async Task DisposeAsync()
    {
        await _session.DisposeAsync();
        await _host.DisposeAsync();
        _vault.Dispose();
    }

    private string LastKey => "maps.last:" + _host.Current!.RootPath;

    private static MemoryStream Bytes(params byte[] bytes) => new(bytes.Length == 0 ? [1, 2, 3] : bytes);

    // El watcher puede reaplicar un instante una nota que ya se borró; el catálogo se estabiliza solo, así que se espera a que lo haga.
    private async Task<MapCatalog> StableCatalogAsync(Func<MapCatalog, bool> isExpected)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        var catalog = await _library.LoadAsync();
        while (!isExpected(catalog) && DateTime.UtcNow < deadline)
        {
            await Task.Delay(25);
            catalog = await _library.LoadAsync();
        }
        return catalog;
    }

    private string Read(string path) => File.ReadAllText(_vault.PathOf(path));

    private bool Exists(string path) => File.Exists(_vault.PathOf(path));

    private void WriteFile(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_vault.PathOf(path))!);
        File.WriteAllText(_vault.PathOf(path), text);
    }

    private async Task<MapOpResult> SeedImageMapAsync()
    {
        var result = await _library.ImportAsync("Faro", ".png", Bytes(9, 9));
        File.WriteAllText(_vault.PathOf("Mapas/Faro.md"), Read("Mapas/Faro.md") + "\nCuerpo con [[Otra nota]].\n");
        File.WriteAllText(_vault.PathOf("Mapas/Faro.state.json"), MapStateSerializer.Write(MapState.Empty));
        await _host.Current!.SaveAsync("Mapas/Faro.md", Read("Mapas/Faro.md"), Core.Vault.Storage.WritePrecondition.Overwrite);
        return result;
    }

    // ---- Crear ----

    [Fact]
    public async Task CreateBlank_writes_a_map_note_without_image_or_state()
    {
        var result = await _library.CreateBlankAsync("Cueva");

        (result, Read("Mapas/Cueva.md").Contains("type: map"), Read("Mapas/Cueva.md").Contains("image:"), Exists("Mapas/Cueva.state.json"))
            .Should().Be((new MapOpResult(MapOpStatus.Ok, "Mapas/Cueva.md"), true, false, false));
    }

    [Fact]
    public async Task CreateBlank_twice_gives_NameTaken()
    {
        await _library.CreateBlankAsync("Cueva");

        (await _library.CreateBlankAsync("Cueva")).Status.Should().Be(MapOpStatus.NameTaken);
    }

    [Theory]
    [InlineData("Con/barra")]
    [InlineData("   ")]
    [InlineData("")]
    [InlineData(".oculto")]
    public async Task CreateBlank_with_an_unusable_name_gives_InvalidName(string name)
    {
        (await _library.CreateBlankAsync(name)).Status.Should().Be(MapOpStatus.InvalidName);
    }

    [Fact]
    public async Task CreateBlank_does_not_leave_files_when_the_name_is_invalid()
    {
        await _library.CreateBlankAsync("Con/barra");

        Directory.Exists(_vault.PathOf("Mapas")).Should().BeFalse();
    }

    // ---- Importar ----

    [Fact]
    public async Task Import_writes_the_image_and_a_note_that_points_to_it()
    {
        var result = await _library.ImportAsync("Faro", ".png", Bytes(7, 7, 7));

        (result.Status, Convert.ToHexString(File.ReadAllBytes(_vault.PathOf("Mapas/Faro.png"))), Read("Mapas/Faro.md").Contains("image: \"Mapas/Faro.png\""))
            .Should().Be((MapOpStatus.Ok, "070707", true));
    }

    [Fact]
    public async Task Import_when_the_image_already_exists_gives_NameTaken_and_touches_nothing()
    {
        WriteFile("Mapas/Faro.png", "previa");

        var result = await _library.ImportAsync("Faro", ".png", Bytes(1));

        (result.Status, Read("Mapas/Faro.png"), Exists("Mapas/Faro.md")).Should().Be((MapOpStatus.NameTaken, "previa", false));
    }

    [Fact]
    public async Task Import_when_the_note_already_exists_keeps_the_existing_note_and_writes_no_image()
    {
        await _library.CreateBlankAsync("Faro");

        var result = await _library.ImportAsync("Faro", ".png", Bytes(1));

        (result.Status, Exists("Mapas/Faro.png")).Should().Be((MapOpStatus.NameTaken, false));
    }

    [Theory]
    [InlineData(".exe")]
    [InlineData("")]
    [InlineData(".png/../x")]
    public async Task Import_with_an_extension_that_is_not_an_image_gives_UnsupportedImage(string extension)
    {
        (await _library.ImportAsync("Faro", extension, Bytes(1))).Status.Should().Be(MapOpStatus.UnsupportedImage);
    }

    [Fact]
    public void UnsupportedImage_message_lists_the_accepted_formats()
    {
        new MapOpResult(MapOpStatus.UnsupportedImage, null).ErrorText.Should().Contain("png, jpg, jpeg, webp, gif");
    }

    [Fact]
    public async Task Import_with_an_invalid_name_and_a_bad_extension_still_says_the_name_is_invalid()
    {
        (await _library.ImportAsync("a/b", ".png", Bytes(1))).Status.Should().Be(MapOpStatus.InvalidName);
    }

    [Fact]
    public async Task Import_accepts_the_extension_without_a_dot_and_in_uppercase()
    {
        await _library.ImportAsync("Faro", "JPG", Bytes(1));

        Exists("Mapas/Faro.jpg").Should().BeTrue();
    }

    // ---- Imágenes huérfanas ----

    [Fact]
    public async Task CreateFromImage_makes_a_map_note_named_after_the_image()
    {
        WriteFile("Mapas/suelto.png", "img");

        var result = await _library.CreateFromImageAsync("Mapas/suelto.png");

        (result, Read("Mapas/suelto.md").Contains("image: \"Mapas/suelto.png\""))
            .Should().Be((new MapOpResult(MapOpStatus.Ok, "Mapas/suelto.md"), true));
    }

    [Fact]
    public async Task Image_is_no_longer_an_orphan_after_CreateFromImage()
    {
        WriteFile("Mapas/suelto.png", "img");
        await _library.CreateFromImageAsync("Mapas/suelto.png");

        (await StableCatalogAsync(c => c.OrphanImages.Count == 0)).OrphanImages.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateFromImage_with_a_note_of_that_name_gives_NameTaken()
    {
        WriteFile("Mapas/suelto.png", "img");
        await _library.CreateBlankAsync("suelto");

        (await _library.CreateFromImageAsync("Mapas/suelto.png")).Status.Should().Be(MapOpStatus.NameTaken);
    }

    [Theory]
    [InlineData("../fuera.png")]
    [InlineData("Otra/suelto.png")]
    public async Task CreateFromImage_rejects_paths_outside_Mapas(string path)
    {
        (await _library.CreateFromImageAsync(path)).Status.Should().Be(MapOpStatus.InvalidName);
    }

    [Fact]
    public async Task CreateFromImage_rejects_files_that_are_not_images_as_UnsupportedImage()
    {
        (await _library.CreateFromImageAsync("Mapas/suelto.txt")).Status.Should().Be(MapOpStatus.UnsupportedImage);
    }

    [Fact]
    public async Task CreateFromImage_accepts_a_folder_with_other_casing()
    {
        WriteFile("mapas/suelto.png", "img");

        var result = await _library.CreateFromImageAsync("mapas/suelto.png");

        (result.Status, Exists("Mapas/suelto.md")).Should().Be((MapOpStatus.Ok, true));
    }

    [Fact]
    public async Task Load_lists_maps_and_orphan_images()
    {
        await _library.CreateBlankAsync("Cueva");
        WriteFile("Mapas/suelto.png", "img");

        var catalog = await _library.LoadAsync();

        (string.Join(",", catalog.Maps.Select(m => m.Name)), string.Join(",", catalog.OrphanImages))
            .Should().Be(("Cueva", "Mapas/suelto.png"));
    }

    // ---- Renombrar ----

    [Fact]
    public async Task Rename_moves_the_note_and_its_state_and_updates_the_name()
    {
        await SeedImageMapAsync();

        var result = await _library.RenameAsync("Mapas/Faro.md", "Torre");

        (result, Exists("Mapas/Faro.md"), Exists("Mapas/Faro.state.json"), Exists("Mapas/Torre.state.json"), Read("Mapas/Torre.md").Contains("name: \"Torre\""))
            .Should().Be((new MapOpResult(MapOpStatus.Ok, "Mapas/Torre.md"), false, false, true, true));
    }

    [Fact]
    public async Task Rename_keeps_the_image_and_the_rest_of_the_body()
    {
        await SeedImageMapAsync();

        await _library.RenameAsync("Mapas/Faro.md", "Torre");

        var text = Read("Mapas/Torre.md");
        (text.Contains("image: \"Mapas/Faro.png\""), text.Contains("Cuerpo con [[Otra nota]]."), Exists("Mapas/Faro.png"))
            .Should().Be((true, true, true));
    }

    [Fact]
    public async Task Rename_refreshes_the_vault_index()
    {
        await SeedImageMapAsync();

        await _library.RenameAsync("Mapas/Faro.md", "Torre");

        (await StableCatalogAsync(c => c.Maps.Count == 1)).Maps.Select(m => m.NotePath).Should().Equal("Mapas/Torre.md");
    }

    [Fact]
    public async Task Rename_of_the_open_map_keeps_it_open_under_the_new_path()
    {
        await SeedImageMapAsync();
        await _session.OpenAsync("Mapas/Faro.md");

        await _library.RenameAsync("Mapas/Faro.md", "Torre");

        _session.Current!.NotePath.Should().Be("Mapas/Torre.md");
    }

    [Fact]
    public async Task Rename_of_the_open_map_saves_pending_changes_under_the_new_state_file()
    {
        await SeedImageMapAsync();
        await _session.OpenAsync("Mapas/Faro.md");
        _session.Mutate(e => e.AddToken(new TokenDraft("Orco", null, TokenSide.Enemy, 1, null, null), 5, 5) is not null);

        await _library.RenameAsync("Mapas/Faro.md", "Torre");

        Read("Mapas/Torre.state.json").Should().Contain("Orco");
    }

    [Fact]
    public async Task Rename_to_a_taken_name_gives_NameTaken_and_moves_nothing()
    {
        await SeedImageMapAsync();
        await _library.CreateBlankAsync("Torre");

        var result = await _library.RenameAsync("Mapas/Faro.md", "Torre");

        (result.Status, Exists("Mapas/Faro.md"), Exists("Mapas/Faro.state.json")).Should().Be((MapOpStatus.NameTaken, true, true));
    }

    [Fact]
    public async Task Rename_to_a_name_whose_state_file_exists_gives_NameTaken()
    {
        await SeedImageMapAsync();
        WriteFile("Mapas/Torre.state.json", "{}");

        (await _library.RenameAsync("Mapas/Faro.md", "Torre")).Status.Should().Be(MapOpStatus.NameTaken);
    }

    [Fact]
    public async Task Rename_with_an_invalid_name_gives_InvalidName()
    {
        await SeedImageMapAsync();

        (await _library.RenameAsync("Mapas/Faro.md", "a/b")).Status.Should().Be(MapOpStatus.InvalidName);
    }

    [Fact]
    public async Task Rename_changing_only_the_case_is_not_NameTaken()
    {
        await _library.CreateBlankAsync("cueva");

        var result = await _library.RenameAsync("Mapas/cueva.md", "Cueva");

        (result.Status, string.Join(",", Directory.EnumerateFiles(_vault.PathOf("Mapas")).Select(Path.GetFileName)))
            .Should().Be((MapOpStatus.Ok, "Cueva.md"));
    }

    [Fact]
    public async Task Rename_to_the_same_name_changes_nothing_and_is_Ok()
    {
        await _library.CreateBlankAsync("Cueva");

        (await _library.RenameAsync("Mapas/Cueva.md", "Cueva")).Should().Be(new MapOpResult(MapOpStatus.Ok, "Mapas/Cueva.md"));
    }

    // ---- Borrar ----

    [Fact]
    public async Task Delete_removes_the_note_and_its_state_but_keeps_the_image()
    {
        await SeedImageMapAsync();

        var result = await _library.DeleteAsync("Mapas/Faro.md");

        (result.Status, Exists("Mapas/Faro.md"), Exists("Mapas/Faro.state.json"), Exists("Mapas/Faro.png"))
            .Should().Be((MapOpStatus.Ok, false, false, true));
    }

    [Fact]
    public async Task Image_of_a_deleted_map_becomes_an_orphan()
    {
        await SeedImageMapAsync();

        await _library.DeleteAsync("Mapas/Faro.md");

        (await StableCatalogAsync(c => c.OrphanImages.Count == 1)).OrphanImages.Should().Equal("Mapas/Faro.png");
    }

    [Fact]
    public async Task Delete_of_the_open_map_closes_it()
    {
        await SeedImageMapAsync();
        await _session.OpenAsync("Mapas/Faro.md");

        await _library.DeleteAsync("Mapas/Faro.md");

        _session.Current.Should().BeNull();
    }

    [Fact]
    public async Task Rename_of_a_map_that_is_not_open_updates_the_last_map_preference()
    {
        await SeedImageMapAsync();
        _prefs.Set(LastKey, "Mapas/Faro.md");

        await _library.RenameAsync("Mapas/Faro.md", "Torre");

        _prefs.Get(LastKey).Should().Be("Mapas/Torre.md");
    }

    [Fact]
    public async Task Rename_of_a_map_that_is_not_the_last_one_keeps_the_last_map_preference()
    {
        await SeedImageMapAsync();
        _prefs.Set(LastKey, "Mapas/Otro.md");

        await _library.RenameAsync("Mapas/Faro.md", "Torre");

        _prefs.Get(LastKey).Should().Be("Mapas/Otro.md");
    }

    [Fact]
    public async Task Failed_rename_keeps_the_last_map_preference()
    {
        await SeedImageMapAsync();
        await _library.CreateBlankAsync("Torre");
        _prefs.Set(LastKey, "Mapas/Faro.md");

        await _library.RenameAsync("Mapas/Faro.md", "Torre");

        _prefs.Get(LastKey).Should().Be("Mapas/Faro.md");
    }

    [Fact]
    public async Task Delete_clears_the_last_map_preference_when_it_points_to_that_map()
    {
        await SeedImageMapAsync();
        _prefs.Set(LastKey, "Mapas/Faro.md");

        await _library.DeleteAsync("Mapas/Faro.md");

        _prefs.Get(LastKey).Should().BeNull();
    }

    [Fact]
    public async Task Delete_keeps_the_last_map_preference_of_another_map()
    {
        await SeedImageMapAsync();
        _prefs.Set(LastKey, "Mapas/Otro.md");

        await _library.DeleteAsync("Mapas/Faro.md");

        _prefs.Get(LastKey).Should().Be("Mapas/Otro.md");
    }

    [Fact]
    public async Task Delete_of_a_note_that_is_not_a_map_is_refused()
    {
        WriteFile("Historia/nota.md", "---\ntype: note\n---\n");
        await _host.Current!.SaveAsync("Historia/nota.md", "---\ntype: note\n---\n", Core.Vault.Storage.WritePrecondition.Overwrite);

        (await _library.DeleteAsync("Historia/nota.md")).Status.Should().Be(MapOpStatus.InvalidName);
    }
}
