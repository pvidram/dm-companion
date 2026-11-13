using DmCompanion.Desktop.Platform;
using Microsoft.Extensions.Configuration;

namespace DmCompanion.Desktop.Tests;

public sealed class DesktopSampleVaultTests : IDisposable
{
    private readonly TempVault _data = TempVault.Empty();
    private readonly TempVault _documents = TempVault.Empty();
    private readonly DesktopPaths _paths;
    private readonly DesktopSampleVault _sample;

    public DesktopSampleVaultTests()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Desktop:DataRoot"] = _data.Root,
            ["Desktop:DocumentsRoot"] = _documents.Root,
            ["Desktop:SampleSource"] = TempVault.FindDemo(),
        }).Build();
        _paths = new DesktopPaths(config);
        _sample = new DesktopSampleVault(_paths);
    }

    public void Dispose()
    {
        _documents.Dispose();
        _data.Dispose();
    }

    [Fact]
    public async Task PrepareAsync_copies_the_sample_and_returns_its_path()
    {
        var path = await _sample.PrepareAsync();

        (path == _paths.SampleTarget && File.Exists(Path.Combine(path, "Campaña.md"))).Should().BeTrue();
    }

    [Fact]
    public async Task PrepareAsync_does_not_overwrite_a_modified_file()
    {
        var path = await _sample.PrepareAsync();
        var note = Path.Combine(path, "Campaña.md");
        File.WriteAllText(note, "mis cambios");

        await _sample.PrepareAsync();

        File.ReadAllText(note).Should().Be("mis cambios");
    }
}
