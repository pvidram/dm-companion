using DmCompanion.Desktop.Window;
using Microsoft.Extensions.Logging.Abstractions;

namespace DmCompanion.Desktop.Tests;

public sealed class WindowPlacementStoreTests : IDisposable
{
    private readonly TempVault _folder = TempVault.Empty();

    public void Dispose() => _folder.Dispose();

    private WindowPlacementStore Store() =>
        new(Path.Combine(_folder.Root, "window.json"), NullLogger<WindowPlacementStore>.Instance);

    [Fact]
    public void Load_returns_null_when_the_file_is_missing() => Store().Load().Should().BeNull();

    [Fact]
    public void Save_then_load_returns_the_same_bounds()
    {
        var bounds = new WindowBounds(10, 20, 1300, 900, true);

        Store().Save(bounds);

        Store().Load().Should().Be(bounds);
    }

    [Fact]
    public void Load_returns_null_when_the_file_is_corrupt()
    {
        File.WriteAllText(Path.Combine(_folder.Root, "window.json"), "{ no es json");

        Store().Load().Should().BeNull();
    }

    [Fact]
    public void Load_returns_null_when_a_size_is_not_positive()
    {
        File.WriteAllText(Path.Combine(_folder.Root, "window.json"),
            """{"X":0,"Y":0,"Width":0,"Height":800,"Maximized":false}""");

        Store().Load().Should().BeNull();
    }
}
