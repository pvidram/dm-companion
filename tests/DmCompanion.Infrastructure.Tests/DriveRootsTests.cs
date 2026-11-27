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
        ], unix: false).Should().Equal("C:\\", "E:\\");

    [Fact]
    public void From_on_unix_keeps_only_user_mounts() =>
        DriveRoots.From([
            new("/", DriveType.Fixed, true),
            new("/sys/kernel/tracing", DriveType.Fixed, true),
            new("/usr/lib/modules/6.18-WSL2", DriveType.Fixed, true),
            new("/mnt/wslg/distro", DriveType.Fixed, true),
            new("/mnt/wsl", DriveType.Fixed, true),
            new("/mnt/c", DriveType.Network, true),
            new("/mnt/d", DriveType.Network, false),
            new("/media/usb", DriveType.Removable, true),
            new("/run/media/user/disk", DriveType.Fixed, true),
            new("/run/user/1000", DriveType.Ram, true),
            new("/mnt/c", DriveType.Network, true),
        ], unix: true).Should().Equal("/mnt/c", "/media/usb", "/run/media/user/disk");
}
