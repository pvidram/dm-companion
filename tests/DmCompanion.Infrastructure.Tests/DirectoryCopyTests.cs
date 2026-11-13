namespace DmCompanion.Infrastructure.Tests;

public sealed class DirectoryCopyTests : IDisposable
{
    private readonly TempVault _source = TempVault.FromDemo();
    private readonly TempVault _parent = TempVault.Empty();

    public void Dispose()
    {
        _source.Dispose();
        _parent.Dispose();
    }

    private string Target => Path.Combine(_parent.Root, "Ejemplo");

    private static Dictionary<string, string> Snapshot(string root) =>
        Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .ToDictionary(f => Path.GetRelativePath(root, f), File.ReadAllText);

    [Fact]
    public async Task Copies_the_tree_when_the_target_is_missing()
    {
        await DirectoryCopy.CopyIfMissingAsync(_source.Root, Target);

        Snapshot(Target).Should().BeEquivalentTo(Snapshot(_source.Root));
    }

    [Fact]
    public async Task Copies_empty_subfolders_too()
    {
        Directory.CreateDirectory(Path.Combine(_source.Root, "Vacia", "Interior"));

        await DirectoryCopy.CopyIfMissingAsync(_source.Root, Target);

        Directory.Exists(Path.Combine(Target, "Vacia", "Interior")).Should().BeTrue();
    }

    [Fact]
    public async Task Returns_true_when_it_copies()
    {
        (await DirectoryCopy.CopyIfMissingAsync(_source.Root, Target)).Should().BeTrue();
    }

    [Fact]
    public async Task Returns_false_when_the_target_exists()
    {
        Directory.CreateDirectory(Target);

        (await DirectoryCopy.CopyIfMissingAsync(_source.Root, Target)).Should().BeFalse();
    }

    [Fact]
    public async Task Leaves_an_existing_target_untouched()
    {
        Directory.CreateDirectory(Target);
        var sentinel = Path.Combine(Target, "centinela.md");
        await File.WriteAllTextAsync(sentinel, "original");

        await DirectoryCopy.CopyIfMissingAsync(_source.Root, Target);

        (await File.ReadAllTextAsync(sentinel)).Should().Be("original");
    }

    [Fact]
    public async Task Does_not_add_files_to_an_existing_target()
    {
        Directory.CreateDirectory(Target);

        await DirectoryCopy.CopyIfMissingAsync(_source.Root, Target);

        Directory.EnumerateFileSystemEntries(Target).Should().BeEmpty();
    }

    [Fact]
    public async Task Leaves_no_temporary_folder_next_to_the_target()
    {
        await DirectoryCopy.CopyIfMissingAsync(_source.Root, Target);

        Directory.GetDirectories(_parent.Root).Should().Equal(Target);
    }
}
