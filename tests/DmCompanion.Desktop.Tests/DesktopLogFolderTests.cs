using System.ComponentModel;
using DmCompanion.Desktop.Platform;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace DmCompanion.Desktop.Tests;

public sealed class DesktopLogFolderTests
{
    [Fact]
    public void Explorer_receives_a_path_with_spaces_as_a_single_argument()
    {
        var info = DesktopLogFolder.ExplorerStartInfo(@"C:\Users\Jane Doe\AppData\Local\DmCompanion\logs");

        info.ArgumentList.Should().Equal(@"C:\Users\Jane Doe\AppData\Local\DmCompanion\logs");
    }

    [Fact]
    public void Explorer_start_info_launches_explorer_exe()
    {
        DesktopLogFolder.ExplorerStartInfo(@"C:\logs").FileName.Should().Be("explorer.exe");
    }

    [Fact]
    public void Open_returns_false_when_the_system_cannot_start_the_explorer()
    {
        using var data = TempVault.Empty();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Desktop:DataRoot"] = data.Root }).Build();
        var folder = new DesktopLogFolder(new DesktopPaths(config), NullLogger<DesktopLogFolder>.Instance, _ => throw new Win32Exception());

        folder.Open().Should().BeFalse();
    }

    [Fact]
    public void Open_returns_true_when_the_explorer_starts()
    {
        using var data = TempVault.Empty();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Desktop:DataRoot"] = data.Root }).Build();
        var folder = new DesktopLogFolder(new DesktopPaths(config), NullLogger<DesktopLogFolder>.Instance, _ => null);

        folder.Open().Should().BeTrue();
    }
}
