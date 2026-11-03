namespace DmCompanion.Infrastructure.Tests;

public sealed class DirectoryBrowserTests : IDisposable
{
    private readonly TempVault _vault = TempVault.FromDemo();
    private readonly DirectoryBrowser _browser = new();

    public void Dispose() => _vault.Dispose();

    [Fact]
    public void ListSubdirectories_returns_visible_folders_sorted() =>
        _browser.ListSubdirectories(_vault.Root).Should().Equal("Historia", "Lugares", "Personajes");

    [Fact]
    public void ListSubdirectories_of_missing_folder_is_empty() =>
        _browser.ListSubdirectories(Path.Combine(_vault.Root, "no-existe")).Should().BeEmpty();
}
