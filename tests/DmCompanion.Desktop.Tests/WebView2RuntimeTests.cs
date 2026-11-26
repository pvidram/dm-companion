using DmCompanion.Desktop.Platform;

namespace DmCompanion.Desktop.Tests;

public sealed class WebView2RuntimeTests
{
    private static Func<string, string?> Only(string hive, string? pv) =>
        path => path.StartsWith(hive + @"\") ? pv : null;

    [Fact]
    public void It_is_installed_when_found_in_the_machine_hive() =>
        WebView2Runtime.IsInstalled(Only("HKLM", "120.0.1")).Should().BeTrue();

    [Fact]
    public void It_is_installed_when_found_only_in_the_user_hive() =>
        WebView2Runtime.IsInstalled(Only("HKCU", "120.0.1")).Should().BeTrue();

    [Fact]
    public void It_is_not_installed_when_the_value_is_empty() =>
        WebView2Runtime.IsInstalled(Only("HKLM", "")).Should().BeFalse();

    [Fact]
    public void It_is_not_installed_when_the_version_is_all_zeros() =>
        WebView2Runtime.IsInstalled(Only("HKLM", "0.0.0.0")).Should().BeFalse();

    [Fact]
    public void It_is_not_installed_when_the_key_is_absent() =>
        WebView2Runtime.IsInstalled(_ => null).Should().BeFalse();
}
