using DmCompanion.Core.Vault;
using DmCompanion.Screens.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace DmCompanion.Screens.Tests.Services;

public sealed class NoteImageResolverTests : IAsyncLifetime
{
    private readonly CountingFactory _factory = new();
    private readonly VaultHost _host;
    private readonly NoteImageResolver _resolver;
    private TempVault? _vault;

    public NoteImageResolverTests()
    {
        _host = new VaultHost(_factory, new InMemoryAppPreferences(), NullLogger<VaultHost>.Instance);
        _resolver = new NoteImageResolver(_host);
    }

    public async Task InitializeAsync()
    {
        _vault = TempVault.FromDemo();
        foreach (var path in new[] { "_assets/faro.jpg", "mapas/unico.png", "a/doble.png", "b/doble.png" })
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_vault.PathOf(path))!);
            File.WriteAllBytes(_vault.PathOf(path), [1, 2, 3]);
        }
        File.WriteAllText(_vault.PathOf("nota.md"), "x");
        (await _host.OpenAsync(_vault.Root)).Should().BeTrue();
        await _resolver.EnsureLoadedAsync();
    }

    public async Task DisposeAsync()
    {
        await _host.DisposeAsync();
        _vault?.Dispose();
    }

    [Fact]
    public void ResolvePath_finds_exact_relative_path() => _resolver.ResolvePath("_assets/faro.jpg").Should().Be("_assets/faro.jpg");

    [Fact]
    public void ResolvePath_ignores_case_in_exact_and_suffix_matches()
    {
        _resolver.ResolvePath("_assets/FARO.JPG").Should().Be("_assets/faro.jpg");
        _resolver.ResolvePath("Mapas/Unico.PNG").Should().Be("mapas/unico.png");
    }

    [Fact]
    public async Task Concurrent_loads_all_see_the_loaded_list()
    {
        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => _resolver.EnsureLoadedAsync()));
        _resolver.ResolvePath("faro.jpg").Should().Be("_assets/faro.jpg");
    }

    [Fact]
    public void ResolvePath_finds_unique_file_name_anywhere() => _resolver.ResolvePath("unico.png").Should().Be("mapas/unico.png");

    [Fact]
    public void ResolvePath_returns_null_for_ambiguous_name() => _resolver.ResolvePath("doble.png").Should().BeNull();

    [Theory]
    [InlineData("../x.jpg")]
    [InlineData("nota.md")]
    public void ResolvePath_rejects_non_image_and_outside_vault(string target) => _resolver.ResolvePath(target).Should().BeNull();

    [Fact]
    public void TabletUrls_returns_virtual_vault_url_with_stamp() =>
        _resolver.TabletUrls()("faro.jpg").Should().StartWith("/_vault/_assets/faro.jpg?v=");

    // Escribe el fichero y espera a que el vault lance su FileChanged (el resolver se suscribió antes, así que ya lo ha visto).
    private async Task WriteAndWaitForChangeAsync(string path)
    {
        var seen = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnChanged(object? sender, Core.Vault.Storage.VaultChange change)
        {
            if (VaultPaths.Normalize(change.RelativePath) == path) seen.TrySetResult();
        }
        _host.Current!.FileChanged += OnChanged;
        try
        {
            File.WriteAllBytes(_vault!.PathOf(path), [4, 5, 6]);
            await seen.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally
        {
            _host.Current!.FileChanged -= OnChanged;
        }
    }

    [Fact]
    public async Task A_non_image_change_does_not_relist_the_vault()
    {
        var before = _factory.FileListings;

        await WriteAndWaitForChangeAsync("mapas/prueba.state.json");
        await _resolver.EnsureLoadedAsync();

        _factory.FileListings.Should().Be(before);
    }

    [Fact]
    public async Task An_image_change_relists_the_vault_on_the_next_load()
    {
        var before = _factory.FileListings;

        await WriteAndWaitForChangeAsync("mapas/nuevo.png");
        await _resolver.EnsureLoadedAsync();

        (_factory.FileListings - before, _resolver.ResolvePath("nuevo.png")).Should().Be((1, "mapas/nuevo.png"));
    }
}
