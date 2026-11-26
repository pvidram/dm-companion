using DmCompanion.Core.Vault.Storage;

namespace DmCompanion.Infrastructure.Tests;

public sealed class DirectoryBrowserTests : IDisposable
{
    private readonly TempVault _vault = TempVault.FromDemo();
    private readonly DirectoryBrowser _browser = new();

    public void Dispose() => _vault.Dispose();

    [Fact]
    public void ListSubdirectories_returns_visible_folders_sorted() =>
        _browser.ListSubdirectories(_vault.Root).Should().Equal("Historia", "Lugares", "manuales", "Mapas", "Personajes");

    [Fact]
    public void ListSubdirectories_of_missing_folder_is_empty() =>
        _browser.ListSubdirectories(Path.Combine(_vault.Root, "no-existe")).Should().BeEmpty();

    [Fact]
    public void DirectoryExists_is_true_for_an_existing_folder() =>
        _browser.DirectoryExists(_vault.Root).Should().BeTrue();

    [Fact]
    public void DirectoryExists_is_false_for_a_missing_folder() =>
        _browser.DirectoryExists(Path.Combine(_vault.Root, "no-existe")).Should().BeFalse();

    [Fact]
    public void LooksLikeVault_is_true_for_a_folder_with_notes() =>
        _browser.LooksLikeVault(_vault.Root).Should().BeTrue();

    [Fact]
    public void LooksLikeVault_is_true_for_a_folder_with_only_a_dot_obsidian_folder()
    {
        using var empty = TempVault.Empty();
        Directory.CreateDirectory(empty.PathOf(".obsidian"));

        _browser.LooksLikeVault(empty.Root).Should().BeTrue();
    }

    [Fact]
    public void LooksLikeVault_is_true_when_the_notes_are_one_level_down()
    {
        using var nested = TempVault.Empty();
        Directory.CreateDirectory(nested.PathOf("Notas"));
        File.WriteAllText(nested.PathOf("Notas/a.md"), "x");

        _browser.LooksLikeVault(nested.Root).Should().BeTrue();
    }

    [Fact]
    public void LooksLikeVault_is_false_for_a_folder_without_notes()
    {
        using var empty = TempVault.Empty();
        File.WriteAllText(empty.PathOf("foto.png"), "x");

        _browser.LooksLikeVault(empty.Root).Should().BeFalse();
    }

    [Fact]
    public void LooksLikeVault_is_false_for_a_drive_root() =>
        _browser.LooksLikeVault(Path.GetPathRoot(_vault.Root)!).Should().BeFalse();

    [Fact]
    public void LooksLikeVault_does_not_warn_about_a_missing_folder() =>
        _browser.LooksLikeVault(Path.Combine(_vault.Root, "no-existe")).Should().BeTrue();

    [Fact]
    public void LooksLikeVault_is_false_for_a_unc_share_root_with_a_trailing_separator()
    {
        // Solo Windows resuelve rutas UNC; en otros sistemas no hay nada que comprobar.
        if (!OperatingSystem.IsWindows()) return;

        _browser.LooksLikeVault(@"\\localhost\C$\").Should().BeFalse();
    }

    [Fact]
    public void Classify_is_Vault_for_a_folder_with_notes() =>
        _browser.Classify(_vault.Root).Should().Be(FolderVaultStatus.Vault);

    [Fact]
    public void Classify_is_Vault_for_a_folder_with_only_a_dot_obsidian_folder()
    {
        using var empty = TempVault.Empty();
        Directory.CreateDirectory(empty.PathOf(".obsidian"));

        _browser.Classify(empty.Root).Should().Be(FolderVaultStatus.Vault);
    }

    [Fact]
    public void Classify_is_Vault_for_a_missing_folder() =>
        _browser.Classify(Path.Combine(_vault.Root, "no-existe")).Should().Be(FolderVaultStatus.Vault);

    [Fact]
    public void Classify_is_Empty_for_a_folder_with_only_non_markdown_files()
    {
        using var empty = TempVault.Empty();
        File.WriteAllText(empty.PathOf("foto.png"), "x");

        _browser.Classify(empty.Root).Should().Be(FolderVaultStatus.Empty);
    }

    [Fact]
    public void Classify_is_DriveRoot_for_the_filesystem_root() =>
        _browser.Classify(Path.GetPathRoot(Path.GetTempPath())!).Should().Be(FolderVaultStatus.DriveRoot);

    [Fact]
    public void Classify_is_Unknown_when_the_entry_limit_is_reached()
    {
        using var big = TempVault.Empty();
        for (var i = 0; i < 2001; i++) File.WriteAllText(big.PathOf($"{i}.txt"), "");

        _browser.Classify(big.Root).Should().Be(FolderVaultStatus.Unknown);
    }

    [Fact]
    public void CreateDirectory_creates_the_folder_and_returns_its_path()
    {
        using var empty = TempVault.Empty();

        var result = _browser.CreateDirectory(empty.Root, "La Marca del Este");

        result.Should().Be(new CreateFolderResult(CreateFolderStatus.Created, Path.Combine(empty.Root, "La Marca del Este")));
        Directory.Exists(result.Path).Should().BeTrue();
    }

    [Fact]
    public void CreateDirectory_trims_the_name()
    {
        using var empty = TempVault.Empty();

        _browser.CreateDirectory(empty.Root, "  Campañas ");

        Directory.Exists(empty.PathOf("Campañas")).Should().BeTrue();
    }

    [Fact]
    public void CreateDirectory_with_an_invalid_name_creates_nothing()
    {
        using var empty = TempVault.Empty();

        var result = _browser.CreateDirectory(empty.Root, "a/b");

        (result.Status, string.IsNullOrEmpty(result.Message), Directory.GetDirectories(empty.Root).Length)
            .Should().Be((CreateFolderStatus.InvalidName, false, 0));
    }

    [Fact]
    public void CreateDirectory_rejects_a_name_that_differs_only_in_case()
    {
        using var empty = TempVault.Empty();
        Directory.CreateDirectory(empty.PathOf("Campañas"));

        _browser.CreateDirectory(empty.Root, "campañas").Status.Should().Be(CreateFolderStatus.AlreadyExists);
    }

    [Fact]
    public void CreateDirectory_rejects_a_name_taken_by_a_file()
    {
        using var empty = TempVault.Empty();
        File.WriteAllText(empty.PathOf("Notas"), "x");

        _browser.CreateDirectory(empty.Root, "Notas").Status.Should().Be(CreateFolderStatus.AlreadyExists);
    }

    [Fact]
    public void CreateDirectory_in_a_missing_parent_is_ParentMissing() =>
        _browser.CreateDirectory(Path.Combine(_vault.Root, "no-existe"), "X").Status.Should().Be(CreateFolderStatus.ParentMissing);
}
