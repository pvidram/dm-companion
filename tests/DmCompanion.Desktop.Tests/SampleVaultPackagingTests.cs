namespace DmCompanion.Desktop.Tests;

/// <summary>The welcome screen needs the sample vault next to the executable in any build, not only in the release script.</summary>
public sealed class SampleVaultPackagingTests
{
    [Fact]
    public void Build_output_contains_every_sample_vault_file()
    {
        var source = FindSampleVault();
        var target = Path.Combine(AppContext.BaseDirectory, "ejemplo");
        var missing = Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(source, file))
            .Where(relative => !File.Exists(Path.Combine(target, relative)))
            .ToList();
        missing.Should().BeEmpty();
    }

    private static string FindSampleVault()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "samples", "vault-demo"))) dir = dir.Parent;
        return Path.Combine(dir!.FullName, "samples", "vault-demo");
    }
}
