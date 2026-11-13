namespace DmCompanion.Infrastructure.Tests;

public sealed class DirectoryBrowserTests : IDisposable
{
    private readonly TempVault _vault = TempVault.FromDemo();
    private readonly DirectoryBrowser _browser = new();

    public void Dispose() => _vault.Dispose();

    [Fact]
    public void ListSubdirectories_returns_visible_folders_sorted() =>
        _browser.ListSubdirectories(_vault.Root).Should().Equal("Historia", "Lugares", "Mapas", "Personajes");

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
}
