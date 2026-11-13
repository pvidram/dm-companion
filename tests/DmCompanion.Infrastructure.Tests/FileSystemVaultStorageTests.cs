using DmCompanion.Core.Vault.Storage;

namespace DmCompanion.Infrastructure.Tests;

public sealed class FileSystemVaultStorageTests : IDisposable
{
    private const string ConflictPath = "Lugares/Puerto Sal.sync-conflict-20261103-101500-ABCDEF1.md";
    private readonly TempVault _vault = TempVault.FromDemo();
    private readonly FileSystemVaultStorage _storage;

    public FileSystemVaultStorageTests() => _storage = new FileSystemVaultStorage(_vault.Root);

    public void Dispose() => _vault.Dispose();

    [Fact]
    public async Task List_returns_only_indexable_markdown() =>
        (await _storage.ListMarkdownFilesAsync()).Should().HaveCount(11);

    [Fact]
    public async Task ListFiles_does_not_enumerate_hidden_folders()
    {
        Directory.CreateDirectory(_vault.PathOf(".oculta"));
        await File.WriteAllTextAsync(_vault.PathOf(".oculta/secreto.md"), "x");
        (await _storage.ListFilesAsync(_ => true)).Select(f => f.RelativePath)
            .Should().NotContain(p => p.Contains(".oculta"));
    }

    [Fact]
    public async Task List_uses_forward_slash_relative_paths() =>
        (await _storage.ListMarkdownFilesAsync()).Select(f => f.RelativePath)
            .Should().Contain("Historia/Sesión 1 - Llegada a Puerto Sal.md");

    [Fact]
    public async Task Read_returns_content_and_timestamp()
    {
        var file = await _storage.ReadAsync("Personajes/Lyra.md");
        (file.Content.Contains("Lyra Vientoalto"), file.LastWriteTimeUtc)
            .Should().Be((true, File.GetLastWriteTimeUtc(_vault.PathOf("Personajes/Lyra.md"))));
    }

    [Fact]
    public async Task Stat_returns_length_and_timestamp()
    {
        var info = await _storage.StatAsync("Personajes/Lyra.md");
        info.Should().Be(new VaultFileInfo(new FileInfo(_vault.PathOf("Personajes/Lyra.md")).Length,
            File.GetLastWriteTimeUtc(_vault.PathOf("Personajes/Lyra.md"))));
    }

    [Fact]
    public async Task Stat_returns_null_for_missing_file() =>
        (await _storage.StatAsync("_assets/lyra.png")).Should().BeNull();

    [Fact]
    public async Task Stat_rejects_paths_outside_the_vault()
    {
        var act = () => _storage.StatAsync("../fuera.png");
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Write_must_not_exist_creates_new_file_and_folders()
    {
        var result = await _storage.WriteAsync("Nueva/Nota.md", "hola", WritePrecondition.MustNotExist);
        (result.Status, File.ReadAllText(_vault.PathOf("Nueva/Nota.md"))).Should().Be((WriteStatus.Written, "hola"));
    }

    [Fact]
    public async Task Write_must_not_exist_refuses_existing_file()
    {
        var original = File.ReadAllText(_vault.PathOf("Personajes/Lyra.md"));
        var result = await _storage.WriteAsync("Personajes/Lyra.md", "pisado", WritePrecondition.MustNotExist);
        (result.Status, File.ReadAllText(_vault.PathOf("Personajes/Lyra.md"))).Should().Be((WriteStatus.AlreadyExists, original));
    }

    [Fact]
    public async Task Write_unchanged_since_succeeds_when_timestamp_matches()
    {
        var file = await _storage.ReadAsync("Personajes/Lyra.md");
        var result = await _storage.WriteAsync(file.RelativePath, "editado", WritePrecondition.UnchangedSince(file.LastWriteTimeUtc));
        result.Status.Should().Be(WriteStatus.Written);
    }

    [Fact]
    public async Task Write_unchanged_since_detects_external_change()
    {
        var file = await _storage.ReadAsync("Personajes/Lyra.md");
        File.WriteAllText(_vault.PathOf(file.RelativePath), "cambio desde Syncthing");
        File.SetLastWriteTimeUtc(_vault.PathOf(file.RelativePath), file.LastWriteTimeUtc.AddMinutes(1));

        var result = await _storage.WriteAsync(file.RelativePath, "mi versión", WritePrecondition.UnchangedSince(file.LastWriteTimeUtc));

        (result.Status, File.ReadAllText(_vault.PathOf(file.RelativePath))).Should().Be((WriteStatus.ModifiedOnDisk, "cambio desde Syncthing"));
    }

    [Fact]
    public async Task Write_leaves_no_temporary_files()
    {
        await _storage.WriteAsync("Personajes/Lyra.md", "x", WritePrecondition.Overwrite);
        Directory.EnumerateFiles(_vault.PathOf("Personajes"), "*.dmc.tmp", SearchOption.AllDirectories).Should().BeEmpty();
    }

    [Fact]
    public async Task FindConflicts_pairs_conflict_with_original() =>
        (await _storage.FindConflictsAsync()).Should().ContainSingle()
            .Which.Should().Be(new SyncConflict("Lugares/Puerto Sal.md", ConflictPath));

    [Fact]
    public async Task Cancelled_write_throws_and_leaves_no_temporary_files()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var act = () => _storage.WriteAsync("Personajes/Nueva.md", "x", WritePrecondition.Overwrite, cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
        Directory.EnumerateFiles(_vault.PathOf("Personajes"), "*.dmc.tmp", SearchOption.AllDirectories).Should().BeEmpty();
    }

    [Fact]
    public async Task Write_unchanged_since_on_deleted_file_reports_modified_and_does_not_recreate()
    {
        var file = await _storage.ReadAsync("Personajes/Lyra.md");
        File.Delete(_vault.PathOf(file.RelativePath));

        var result = await _storage.WriteAsync(file.RelativePath, "x", WritePrecondition.UnchangedSince(file.LastWriteTimeUtc));

        (result.Status, File.Exists(_vault.PathOf(file.RelativePath))).Should().Be((WriteStatus.ModifiedOnDisk, false));
    }

    [Fact]
    public async Task Concurrent_writes_to_same_file_leave_no_temporary_files()
    {
        await Task.WhenAll(Enumerable.Range(0, 8).Select(i =>
            _storage.WriteAsync("Personajes/Lyra.md", $"v{i}", WritePrecondition.Overwrite)));
        Directory.EnumerateFiles(_vault.Root, "*.dmc.tmp", SearchOption.AllDirectories).Should().BeEmpty();
    }

    [Fact]
    public async Task Missing_file_with_unchanged_since_is_not_created()
    {
        var result = await _storage.WriteAsync("Nueva/Otra.md", "x", WritePrecondition.UnchangedSince(DateTime.UtcNow));
        (result.Status, File.Exists(_vault.PathOf("Nueva/Otra.md"))).Should().Be((WriteStatus.ModifiedOnDisk, false));
    }

    [Fact]
    public async Task Delete_removes_file()
    {
        await _storage.DeleteAsync(ConflictPath);
        File.Exists(_vault.PathOf(ConflictPath)).Should().BeFalse();
    }

    [Fact]
    public async Task Paths_outside_the_vault_are_rejected()
    {
        var act = () => _storage.ReadAsync("../fuera.md");
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public void Missing_root_is_rejected()
    {
        var act = () => new FileSystemVaultStorage(Path.Combine(_vault.Root, "no-existe"));
        act.Should().Throw<DirectoryNotFoundException>();
    }

    private static MemoryStream Png(int bytes)
    {
        var data = new byte[bytes];
        new Random(42).NextBytes(data);
        return new MemoryStream(data);
    }

    private IEnumerable<string> TempFiles() =>
        Directory.EnumerateFiles(_vault.Root, "*.tmp", SearchOption.AllDirectories);

    [Fact]
    public async Task WriteBytes_MustNotExist_writes_a_3MB_file()
    {
        using var png = Png(3 * 1024 * 1024);
        var result = await _storage.WriteBytesAsync("Mapas/x.png", png, WritePrecondition.MustNotExist);
        result.Status.Should().Be(WriteStatus.Written);
        (await _storage.StatAsync("Mapas/x.png"))!.Length.Should().Be(3 * 1024 * 1024);
        TempFiles().Should().BeEmpty();
    }

    [Fact]
    public async Task WriteBytes_MustNotExist_on_existing_file_leaves_it_untouched()
    {
        using var first = Png(1000);
        await _storage.WriteBytesAsync("Mapas/x.png", first, WritePrecondition.MustNotExist);
        var before = await File.ReadAllBytesAsync(_vault.PathOf("Mapas/x.png"));

        using var second = Png(2000);
        var result = await _storage.WriteBytesAsync("Mapas/x.png", second, WritePrecondition.MustNotExist);

        result.Status.Should().Be(WriteStatus.AlreadyExists);
        (await File.ReadAllBytesAsync(_vault.PathOf("Mapas/x.png"))).Should().Equal(before);
        TempFiles().Should().BeEmpty();
    }

    [Fact]
    public async Task OpenRead_returns_the_same_bytes()
    {
        using var png = Png(5000);
        var expected = png.ToArray();
        await _storage.WriteBytesAsync("Mapas/x.png", png, WritePrecondition.MustNotExist);
        await using var stream = await _storage.OpenReadAsync("Mapas/x.png");
        using var copy = new MemoryStream();
        await stream.CopyToAsync(copy);
        copy.ToArray().Should().Equal(expected);
    }

    [Fact]
    public async Task Move_renames_a_note()
    {
        await _storage.WriteAsync("Mapas/A.md", "a", WritePrecondition.MustNotExist);
        (await _storage.MoveAsync("Mapas/A.md", "Mapas/B.md")).Should().BeTrue();
        File.Exists(_vault.PathOf("Mapas/A.md")).Should().BeFalse();
        (await File.ReadAllTextAsync(_vault.PathOf("Mapas/B.md"))).Should().Be("a");
    }

    [Fact]
    public async Task Move_returns_false_and_keeps_both_when_destination_exists()
    {
        await _storage.WriteAsync("Mapas/A.md", "a", WritePrecondition.MustNotExist);
        await _storage.WriteAsync("Mapas/B.md", "b", WritePrecondition.MustNotExist);
        (await _storage.MoveAsync("Mapas/A.md", "Mapas/B.md")).Should().BeFalse();
        (await File.ReadAllTextAsync(_vault.PathOf("Mapas/A.md"))).Should().Be("a");
        (await File.ReadAllTextAsync(_vault.PathOf("Mapas/B.md"))).Should().Be("b");
    }

    [Fact]
    public async Task Move_accepts_a_rename_that_only_changes_case()
    {
        await _storage.WriteAsync("Cambios/cueva.md", "a", WritePrecondition.MustNotExist);
        (await _storage.MoveAsync("Cambios/cueva.md", "Cambios/Cueva.md")).Should().BeTrue();
        Directory.EnumerateFiles(_vault.PathOf("Cambios")).Select(Path.GetFileName).Should().Equal("Cueva.md");
    }

    [Fact]
    public async Task Move_keeps_the_content_when_it_only_changes_case()
    {
        await _storage.WriteAsync("Mapas/cueva.md", "a", WritePrecondition.MustNotExist);
        await _storage.MoveAsync("Mapas/cueva.md", "Mapas/Cueva.md");
        (await File.ReadAllTextAsync(_vault.PathOf("Mapas/Cueva.md"))).Should().Be("a");
    }

    [Fact]
    public async Task Move_changing_case_returns_false_when_another_file_already_has_that_exact_name()
    {
        await _storage.WriteAsync("Mapas/cueva.md", "a", WritePrecondition.MustNotExist);
        await _storage.WriteAsync("Mapas/Cueva.md", "b", WritePrecondition.MustNotExist);
        if (!File.Exists(_vault.PathOf("Mapas/cueva.md")) || Directory.GetFiles(_vault.PathOf("Mapas")).Length < 2) return; // sistema que no distingue mayúsculas
        (await _storage.MoveAsync("Mapas/cueva.md", "Mapas/Cueva.md")).Should().BeFalse();
    }

    [Fact]
    public async Task Move_to_the_same_path_returns_false()
    {
        await _storage.WriteAsync("Mapas/cueva.md", "a", WritePrecondition.MustNotExist);
        (await _storage.MoveAsync("Mapas/cueva.md", "Mapas/cueva.md")).Should().BeFalse();
    }

    [Fact]
    public async Task ListFiles_filters_by_predicate_and_skips_hidden()
    {
        using var png = Png(10);
        await _storage.WriteBytesAsync("Mapas/x.png", png, WritePrecondition.MustNotExist);
        Directory.CreateDirectory(_vault.PathOf(".obsidian"));
        await File.WriteAllBytesAsync(_vault.PathOf(".obsidian/y.png"), [1]);
        var files = await _storage.ListFilesAsync(path => path.EndsWith(".png", StringComparison.Ordinal));
        files.Select(f => f.RelativePath).Should().BeEquivalentTo(["Mapas/Plano del Faro.png", "Mapas/suelto.png", "Mapas/x.png"]);
    }

    [Theory]
    [InlineData("read")]
    [InlineData("open")]
    [InlineData("openAbsolute")]
    [InlineData("openBackslash")]
    [InlineData("stat")]
    [InlineData("write")]
    [InlineData("move")]
    [InlineData("delete")]
    public async Task Unsafe_paths_are_rejected_in_every_operation(string operation)
    {
        Func<Task> act = operation switch
        {
            "read" => () => _storage.ReadAsync("../fuera.md"),
            "open" => () => _storage.OpenReadAsync("../fuera.png"),
            "openAbsolute" => () => _storage.OpenReadAsync("/etc/passwd"),
            "openBackslash" => () => _storage.OpenReadAsync(@"Mapas/..\..\x.png"),
            "stat" => () => _storage.StatAsync(".obsidian/app.json"),
            "write" => () => _storage.WriteAsync("../fuera.md", "x", WritePrecondition.Overwrite),
            "move" => () => _storage.MoveAsync("Lugares/Puerto Sal.md", "../fuera.md"),
            _ => () => _storage.DeleteAsync("../fuera.md"),
        };
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Symlink_pointing_outside_the_vault_is_rejected()
    {
        using var outside = TempVault.Empty();
        await File.WriteAllTextAsync(outside.PathOf("secreto.md"), "secreto");
        Directory.CreateDirectory(_vault.PathOf("Mapas"));
        try
        {
            File.CreateSymbolicLink(_vault.PathOf("Mapas/enlace.md"), outside.PathOf("secreto.md"));
            Directory.CreateSymbolicLink(_vault.PathOf("Mapas/carpeta"), outside.Root);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or PlatformNotSupportedException)
        {
            return; // el SO no permite crear enlaces simbólicos: nada que comprobar
        }

        await FluentActions.Awaiting(() => _storage.ReadAsync("Mapas/enlace.md")).Should().ThrowAsync<UnauthorizedAccessException>();
        await FluentActions.Awaiting(() => _storage.OpenReadAsync("Mapas/carpeta/secreto.md")).Should().ThrowAsync<UnauthorizedAccessException>();
        (await _storage.ListFilesAsync(_ => true)).Select(f => f.RelativePath).Should().NotContain(["Mapas/enlace.md", "Mapas/carpeta/secreto.md"]);
    }

    [Fact]
    public async Task FindConflicts_lists_state_file_conflicts()
    {
        const string conflict = "Mapas/X.sync-conflict-20261112-101500-ABCDEF1.state.json";
        await _storage.WriteAsync(conflict, "{}", WritePrecondition.MustNotExist);
        (await _storage.FindConflictsAsync()).Should().Contain(new SyncConflict("Mapas/X.state.json", conflict));
    }

    private bool TrySymlink(string link, string target, bool directory = false)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(link)!);
            if (directory) Directory.CreateSymbolicLink(link, target); else File.CreateSymbolicLink(link, target);
            return true;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or PlatformNotSupportedException)
        {
            return false; // el SO no permite enlaces simbólicos
        }
    }

    [Fact]
    public async Task Dangling_symlink_is_rejected()
    {
        using var outside = TempVault.Empty();
        if (!TrySymlink(_vault.PathOf("Mapas/colgante.md"), outside.PathOf("no-existe.md"))) return;

        await FluentActions.Awaiting(() => _storage.WriteAsync("Mapas/colgante.md", "x", WritePrecondition.Overwrite))
            .Should().ThrowAsync<UnauthorizedAccessException>();
        File.Exists(outside.PathOf("no-existe.md")).Should().BeFalse();
    }

    [Fact]
    public async Task Chained_symlinks_escaping_the_vault_are_rejected()
    {
        using var outside = TempVault.Empty();
        await File.WriteAllTextAsync(outside.PathOf("secret"), "secreto");
        // root/b -> <fuera>; root/a -> b/secret (relativo): ResolveLinkTarget aparenta destino dentro.
        if (!TrySymlink(_vault.PathOf("b"), outside.Root, directory: true)) return;
        if (!TrySymlink(_vault.PathOf("a"), "b/secret")) return;

        await FluentActions.Awaiting(() => _storage.ReadAsync("a")).Should().ThrowAsync<UnauthorizedAccessException>();
        await FluentActions.Awaiting(() => _storage.OpenReadAsync("a")).Should().ThrowAsync<UnauthorizedAccessException>();
        (await _storage.ListFilesAsync(_ => true)).Select(f => f.RelativePath).Should().NotContain(["a", "b/secret"]);
    }

    [Fact]
    public async Task Symlinks_pointing_inside_the_vault_are_also_rejected()
    {
        if (!TrySymlink(_vault.PathOf("Lugares/atajo.md"), _vault.PathOf("Lugares/Puerto Sal.md"))) return;
        if (!TrySymlink(_vault.PathOf("Atajo"), _vault.PathOf("Lugares"), directory: true)) return;

        await FluentActions.Awaiting(() => _storage.ReadAsync("Lugares/atajo.md")).Should().ThrowAsync<UnauthorizedAccessException>();
        await FluentActions.Awaiting(() => _storage.ReadAsync("Atajo/Puerto Sal.md")).Should().ThrowAsync<UnauthorizedAccessException>();
        (await _storage.ListFilesAsync(_ => true)).Select(f => f.RelativePath).Should().NotContain(["Lugares/atajo.md", "Atajo/Puerto Sal.md"]);
    }

    [Fact]
    public async Task Listings_skip_unsafe_or_linked_files_without_throwing()
    {
        TrySymlink(_vault.PathOf("Lugares/enlace.md"), _vault.PathOf("Lugares/Puerto Sal.md"));
        await File.WriteAllTextAsync(_vault.PathOf("Lugares/raro:nombre.md"), "x");
        await File.WriteAllTextAsync(_vault.PathOf("Lugares/Puerto Sal.sync-conflict-20261103-101500-BBBBBB2.md"), "x");

        var notes = (await _storage.ListMarkdownFilesAsync()).Select(f => f.RelativePath).ToList();
        notes.Should().HaveCount(11);
        notes.Should().NotContain(["Lugares/enlace.md", "Lugares/raro:nombre.md"]);
        (await _storage.FindConflictsAsync()).Should().NotBeEmpty();
    }

    [Fact]
    public async Task FindConflicts_lists_the_real_syncthing_format_for_state_files()
    {
        const string conflict = "Mapas/X.state.sync-conflict-20261112-101500-ABCDEF1.json";
        await _storage.WriteAsync(conflict, "{}", WritePrecondition.MustNotExist);
        (await _storage.FindConflictsAsync()).Should().Contain(new SyncConflict("Mapas/X.state.json", conflict));
    }
}
