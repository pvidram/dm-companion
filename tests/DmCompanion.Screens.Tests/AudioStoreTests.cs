using DmCompanion.Core.Audio;
using DmCompanion.Infrastructure;
using DmCompanion.Screens.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DmCompanion.Screens.Tests;

public sealed class AudioStoreTests : IAsyncDisposable
{
    private readonly TempVault _vault = TempVault.Empty();
    private readonly VaultHost _host = new(new FileSystemVaultServiceFactory(NullLoggerFactory.Instance), new InMemoryAppPreferences(), NullLogger<VaultHost>.Instance);

    public async ValueTask DisposeAsync()
    {
        await _host.DisposeAsync();
        _vault.Dispose();
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition() && DateTime.UtcNow < deadline) await Task.Delay(50);
        condition().Should().BeTrue();
    }

    private async Task<AudioStore> OpenStoreAsync()
    {
        // La carpeta existe antes de abrir: un fichero escrito justo tras crear una carpeta nueva puede escaparse al watcher.
        Directory.CreateDirectory(_vault.PathOf("_audio/_dm/scenes"));
        await _host.OpenAsync(_vault.Root);
        return new AudioStore(_host, NullLogger<AudioStore>.Instance);
    }

    private void WriteFile(string relative, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_vault.PathOf(relative))!);
        File.WriteAllText(_vault.PathOf(relative), content);
    }

    private static AudioScene Scene(string name) =>
        new(name, new SceneBackground(new TrackBackground("a.mp3"), 0.5), [new SceneEffect("fx.ogg", 1, EffectRepeat.Loop)], 0.8);

    private string[] ScenesOnDisk() =>
        Directory.GetFiles(_vault.PathOf("_audio/_dm/scenes")).Select(Path.GetFileName).OfType<string>().Order().ToArray();

    [Fact]
    public async Task Saves_and_lists_scenes()
    {
        using var store = await OpenStoreAsync();

        var result = await store.SaveSceneAsync(Scene("Taberna"), overwrite: false);

        result.Should().Be(StoreResult.Ok);
        store.SceneNames.Should().Equal("Taberna");
        (await store.LoadSceneAsync("Taberna"))!.MasterVolume.Should().Be(0.8);
    }

    [Fact]
    public async Task Save_without_overwrite_reports_already_exists()
    {
        using var store = await OpenStoreAsync();
        await store.SaveSceneAsync(Scene("Taberna"), overwrite: false);

        (await store.SaveSceneAsync(Scene("Taberna"), overwrite: false)).Should().Be(StoreResult.AlreadyExists);
    }

    [Fact]
    public async Task Save_with_overwrite_replaces_the_scene()
    {
        using var store = await OpenStoreAsync();
        await store.SaveSceneAsync(Scene("Taberna"), overwrite: false);

        await store.SaveSceneAsync(Scene("Taberna") with { MasterVolume = 0.1 }, overwrite: true);

        (await store.LoadSceneAsync("Taberna"))!.MasterVolume.Should().Be(0.1);
    }

    [Theory]
    [InlineData("../x")]
    [InlineData("a:b")]
    [InlineData("..\\..\\fuera")]
    public async Task Unsafe_names_never_leave_the_scenes_folder(string name)
    {
        using var store = await OpenStoreAsync();

        await store.SaveSceneAsync(Scene(name), overwrite: false);

        var all = Directory.GetFiles(_vault.Root, "*", SearchOption.AllDirectories).Select(path => Path.GetRelativePath(_vault.Root, path).Replace('\\', '/')).ToList();
        all.Should().ContainSingle().Which.Should().StartWith("_audio/_dm/scenes/").And.EndWith(".json");
    }

    [Fact]
    public async Task Unsafe_name_is_sanitized_in_the_listing()
    {
        using var store = await OpenStoreAsync();

        await store.SaveSceneAsync(Scene("a:b"), overwrite: false);

        store.SceneNames.Should().Equal("ab");
    }

    [Fact]
    public async Task Invalid_scene_file_is_ignored()
    {
        WriteFile("_audio/_dm/scenes/rota.json", "{no es json");
        WriteFile("_audio/_dm/scenes/buena.json", AudioSceneSerializer.Write(Scene("buena")));
        using var store = await OpenStoreAsync();

        await WaitForAsync(() => store.SceneNames.Count > 0);

        store.SceneNames.Should().Equal("buena");
    }

    [Fact]
    public async Task Conflict_files_are_not_scenes()
    {
        WriteFile("_audio/_dm/scenes/buena.json", AudioSceneSerializer.Write(Scene("buena")));
        WriteFile("_audio/_dm/scenes/buena.sync-conflict-20261115-101010-ABCDEFG.json", AudioSceneSerializer.Write(Scene("buena")));
        using var store = await OpenStoreAsync();

        await WaitForAsync(() => store.SceneNames.Count > 0);

        store.SceneNames.Should().Equal("buena");
    }

    [Fact]
    public async Task Scenes_written_outside_the_app_are_picked_up()
    {
        using var store = await OpenStoreAsync();

        WriteFile("_audio/_dm/scenes/externa.json", AudioSceneSerializer.Write(Scene("externa")));

        await WaitForAsync(() => store.SceneNames.Contains("externa"));
    }

    [Fact]
    public async Task Rename_keeps_content()
    {
        using var store = await OpenStoreAsync();
        await store.SaveSceneAsync(Scene("Taberna"), overwrite: false);

        var result = await store.RenameSceneAsync("Taberna", "Posada");

        result.Should().Be(StoreResult.Ok);
        store.SceneNames.Should().Equal("Posada");
        ScenesOnDisk().Should().Equal("Posada.json");
        (await store.LoadSceneAsync("Posada"))!.MasterVolume.Should().Be(0.8);
    }

    [Fact]
    public async Task Rename_reports_already_exists_and_changes_nothing()
    {
        using var store = await OpenStoreAsync();
        await store.SaveSceneAsync(Scene("Taberna"), overwrite: false);
        await store.SaveSceneAsync(Scene("Posada"), overwrite: false);

        var result = await store.RenameSceneAsync("Taberna", "Posada");

        result.Should().Be(StoreResult.AlreadyExists);
        ScenesOnDisk().Should().Equal("Posada.json", "Taberna.json");
    }

    [Fact]
    public async Task Rename_of_a_missing_scene_reports_not_found()
    {
        using var store = await OpenStoreAsync();

        (await store.RenameSceneAsync("Nada", "Otra")).Should().Be(StoreResult.NotFound);
    }

    [Fact]
    public async Task Delete_removes_the_scene()
    {
        using var store = await OpenStoreAsync();
        await store.SaveSceneAsync(Scene("Taberna"), overwrite: false);

        var result = await store.DeleteSceneAsync("Taberna");

        result.Should().Be(StoreResult.Ok);
        store.SceneNames.Should().BeEmpty();
        ScenesOnDisk().Should().BeEmpty();
    }

    [Fact]
    public async Task Delete_of_a_missing_scene_reports_not_found()
    {
        using var store = await OpenStoreAsync();

        (await store.DeleteSceneAsync("Nada")).Should().Be(StoreResult.NotFound);
    }

    [Fact]
    public async Task Favorites_keep_marking_order()
    {
        using var store = await OpenStoreAsync();

        await store.ToggleFavoriteAsync("b.ogg");
        await store.ToggleFavoriteAsync("a.mp3");
        await store.ToggleFavoriteAsync("c.wav");

        store.Favorites.Should().Equal("b.ogg", "a.mp3", "c.wav");
    }

    [Fact]
    public async Task Toggling_a_favorite_twice_removes_it()
    {
        using var store = await OpenStoreAsync();
        await store.ToggleFavoriteAsync("a.mp3");
        await store.ToggleFavoriteAsync("b.mp3");

        await store.ToggleFavoriteAsync("A.mp3");

        store.Favorites.Should().Equal("b.mp3");
    }

    [Fact]
    public async Task Favorites_persist_across_stores()
    {
        using (var first = await OpenStoreAsync()) await first.ToggleFavoriteAsync("a.mp3");

        using var second = new AudioStore(_host, NullLogger<AudioStore>.Instance);

        await WaitForAsync(() => second.Favorites.Count == 1);
    }

    [Fact]
    public async Task Invalid_favorites_file_is_ignored()
    {
        WriteFile("_audio/_dm/favorites.json", "basura");
        WriteFile("_audio/_dm/scenes/marca.json", AudioSceneSerializer.Write(Scene("marca")));
        using var store = await OpenStoreAsync();
        await WaitForAsync(() => store.SceneNames.Count > 0);

        store.Favorites.Should().BeEmpty();
    }

    [Fact]
    public async Task Toggling_with_an_invalid_favorites_file_does_not_overwrite_it()
    {
        WriteFile("_audio/_dm/favorites.json", "basura");
        WriteFile("_audio/_dm/scenes/marca.json", AudioSceneSerializer.Write(Scene("marca")));
        using var store = await OpenStoreAsync();
        await WaitForAsync(() => store.SceneNames.Count > 0);

        await store.ToggleFavoriteAsync("a.mp3");

        File.ReadAllText(_vault.PathOf("_audio/_dm/favorites.json")).Should().Be("basura");
    }

    [Fact]
    public async Task Toggling_with_an_invalid_favorites_file_fails()
    {
        WriteFile("_audio/_dm/favorites.json", "basura");
        WriteFile("_audio/_dm/scenes/marca.json", AudioSceneSerializer.Write(Scene("marca")));
        using var store = await OpenStoreAsync();
        await WaitForAsync(() => store.SceneNames.Count > 0);

        var result = await store.ToggleFavoriteAsync("a.mp3");

        result.Should().Be(StoreResult.Failed);
    }

    [Fact]
    public async Task Toggling_works_again_once_the_favorites_file_is_valid()
    {
        WriteFile("_audio/_dm/favorites.json", "basura");
        using var store = await OpenStoreAsync();
        await WaitForAsync(() => store.SceneNames.Count == 0);
        WriteFile("_audio/_dm/favorites.json", "{\"version\":1,\"tracks\":[\"b.mp3\"]}");
        await WaitForAsync(() => store.Favorites.Count == 1);

        await store.ToggleFavoriteAsync("a.mp3");

        store.Favorites.Should().Equal("b.mp3", "a.mp3");
    }

    [Fact]
    public async Task CountNotesCiting_counts_notes_with_that_ambience()
    {
        WriteFile("Lugares/Taberna.md", "---\ntype: location\nname: Taberna\nambience: taberna\n---\n");
        WriteFile("Lugares/Posada.md", "---\ntype: location\nname: Posada\nambience: Taberna\n---\n");
        WriteFile("Lugares/Bosque.md", "---\ntype: location\nname: Bosque\nambience: Bosque\n---\n");
        using var store = await OpenStoreAsync();
        await store.SaveSceneAsync(Scene("Taberna"), overwrite: false);

        store.CountNotesCiting("Taberna").Should().Be(2);
    }

    [Fact]
    public async Task Write_failure_returns_failed()
    {
        using var store = await OpenStoreAsync();
        File.Delete(_vault.PathOf("_audio/_dm/scenes/.keep"));
        Directory.Delete(_vault.PathOf("_audio/_dm/scenes"));
        File.WriteAllText(_vault.PathOf("_audio/_dm/scenes"), "no soy una carpeta");

        (await store.SaveSceneAsync(Scene("Taberna"), overwrite: false)).Should().Be(StoreResult.Failed);
    }

    [Fact]
    public async Task Save_without_overwrite_treats_names_differing_in_case_as_existing()
    {
        using var store = await OpenStoreAsync();
        await store.SaveSceneAsync(Scene("Taberna"), overwrite: false);

        (await store.SaveSceneAsync(Scene("taberna"), overwrite: false)).Should().Be(StoreResult.AlreadyExists);
    }

    [Fact]
    public async Task Overwrite_keeps_the_existing_file_name_when_case_differs()
    {
        using var store = await OpenStoreAsync();
        await store.SaveSceneAsync(Scene("Taberna"), overwrite: false);

        await store.SaveSceneAsync(Scene("taberna"), overwrite: true);

        ScenesOnDisk().Should().Equal("Taberna.json");
    }

    [Fact]
    public async Task Rename_to_a_name_differing_only_in_case_of_another_scene_reports_already_exists()
    {
        using var store = await OpenStoreAsync();
        await store.SaveSceneAsync(Scene("Taberna"), overwrite: false);
        await store.SaveSceneAsync(Scene("Posada"), overwrite: false);

        (await store.RenameSceneAsync("Taberna", "posada")).Should().Be(StoreResult.AlreadyExists);
    }

    [Fact]
    public async Task Unreadable_file_is_skipped_and_the_rest_loads()
    {
        WriteFile("_audio/_dm/scenes/buena.json", AudioSceneSerializer.Write(Scene("buena")));
        WriteFile("_audio/_dm/scenes/cerrada.json", AudioSceneSerializer.Write(Scene("cerrada")));
        if (OperatingSystem.IsWindows()) return;
        File.SetUnixFileMode(_vault.PathOf("_audio/_dm/scenes/cerrada.json"), UnixFileMode.None);
        try
        {
            if (CanRead("_audio/_dm/scenes/cerrada.json")) return; // root ignora los permisos
            using var store = await OpenStoreAsync();

            await WaitForAsync(() => store.SceneNames.Count > 0);

            store.SceneNames.Should().Equal("buena");
        }
        finally
        {
            File.SetUnixFileMode(_vault.PathOf("_audio/_dm/scenes/cerrada.json"), UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    [Fact]
    public async Task Favorites_conflict_file_is_logged_by_relative_path()
    {
        WriteFile("_audio/_dm/favorites.sync-conflict-20261115-101010-ABCDEFG.json", "{}");
        WriteFile("_audio/_dm/scenes/marca.json", AudioSceneSerializer.Write(Scene("marca")));
        var logger = new CapturingLogger();
        Directory.CreateDirectory(_vault.PathOf("_audio/_dm/scenes"));
        await _host.OpenAsync(_vault.Root);
        using var store = new AudioStore(_host, logger);

        await WaitForAsync(() => store.SceneNames.Count > 0);

        logger.Messages.Should().Contain(message => message.Contains("_audio/_dm/favorites.sync-conflict-20261115-101010-ABCDEFG.json"));
    }

    private bool CanRead(string relative)
    {
        try
        {
            File.ReadAllText(_vault.PathOf(relative));
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private sealed class CapturingLogger : ILogger<AudioStore>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            lock (Messages) Messages.Add(formatter(state, exception));
        }
    }

    [Fact]
    public async Task Without_a_vault_everything_fails_softly()
    {
        using var store = new AudioStore(_host, NullLogger<AudioStore>.Instance);

        (await store.SaveSceneAsync(Scene("x"), overwrite: false)).Should().Be(StoreResult.Failed);
    }
}
