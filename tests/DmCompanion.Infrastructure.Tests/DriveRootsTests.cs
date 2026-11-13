namespace DmCompanion.Infrastructure.Tests;

public sealed class DriveRootsTests
{
    [Fact]
    public void From_keeps_ready_fixed_and_removable_drives_in_order() =>
        DriveRoots.From([
            new("C:\\", DriveType.Fixed, true),
            new("D:\\", DriveType.CDRom, true),
            new("E:\\", DriveType.Removable, true),
            new("F:\\", DriveType.Fixed, false),
            new("Z:\\", DriveType.Network, true),
        ]).Should().Equal("C:\\", "E:\\");
}
