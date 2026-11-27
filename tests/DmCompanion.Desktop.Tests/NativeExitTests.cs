using DmCompanion.Desktop.Window;

namespace DmCompanion.Desktop.Tests;

public sealed class NativeExitTests
{
    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    public void Native_teardown_is_skipped_only_after_a_normal_window_close_on_linux(bool isLinux, bool windowClosedNormally, bool expected) =>
        NativeExit.ShouldSkipNativeTeardown(isLinux, windowClosedNormally).Should().Be(expected);
}
