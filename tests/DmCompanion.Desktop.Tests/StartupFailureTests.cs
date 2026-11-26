namespace DmCompanion.Desktop.Tests;

public sealed class StartupFailureTests : IDisposable
{
    private readonly TempVault _folder = TempVault.Empty();

    public void Dispose() => _folder.Dispose();

    [Fact]
    public void Report_shows_the_message()
    {
        var shown = new List<string>();

        StartupFailure.Report("No se pudo arrancar.", null, null, shown.Add);

        shown.Should().Equal("No se pudo arrancar.");
    }

    [Fact]
    public void Report_returns_a_non_zero_exit_code() =>
        StartupFailure.Report("x", null, null, _ => { }).Should().NotBe(0);

    [Fact]
    public void Report_logs_only_the_exception_type()
    {
        using var provider = new Logging.DailyFileLoggerProvider(_folder.Root, TimeProvider.System);

        StartupFailure.Report("x", provider.CreateLogger("DmCompanion.Desktop"), new IOException("C:\\secreto"), _ => { });

        string.Concat(Directory.GetFiles(_folder.Root).Select(File.ReadAllText)).Should().Contain("IOException").And.NotContain("secreto");
    }

    [Fact]
    public void A_failure_after_startup_does_not_say_it_could_not_start() =>
        StartupFailure.UnexpectedFailureMessage(hasStarted: true, "/logs").Should().Contain("se ha detenido por un error").And.NotContain("no ha podido arrancar");

    [Fact]
    public void The_failure_message_names_the_logs_folder() =>
        StartupFailure.UnexpectedFailureMessage(hasStarted: true, "/mis/registros").Should().Contain("/mis/registros");

    [Fact]
    public void A_failure_before_startup_says_it_could_not_start() =>
        StartupFailure.UnexpectedFailureMessage(hasStarted: false, "/logs").Should().Contain("no ha podido arrancar");

    [Fact]
    public void InstallationCheck_fails_without_wwwroot() => InstallationCheck.IsComplete(_folder.Root).Should().BeFalse();

    [Fact]
    public void InstallationCheck_passes_with_wwwroot()
    {
        Directory.CreateDirectory(_folder.PathOf("wwwroot"));

        InstallationCheck.IsComplete(_folder.Root).Should().BeTrue();
    }

    [Fact]
    public void InstallationCheck_passes_in_development_with_the_static_assets_manifest()
    {
        File.WriteAllText(_folder.PathOf("DMCompanion.staticwebassets.runtime.json"), "{}");

        InstallationCheck.IsComplete(_folder.Root).Should().BeTrue();
    }

    [Fact]
    public void A_missing_native_library_on_Linux_reports_WebKitGTK() =>
        StartupFailure.MissingNativeLibraryMessage(new DllNotFoundException(), true).Should().Contain("libwebkit2gtk-4.1");

    [Fact]
    public void A_missing_native_library_wrapped_in_another_exception_reports_WebKitGTK() =>
        StartupFailure.MissingNativeLibraryMessage(new TypeInitializationException("X", new DllNotFoundException()), true).Should().NotBeNull();

    [Fact]
    public void A_missing_native_library_off_Linux_has_no_special_message() =>
        StartupFailure.MissingNativeLibraryMessage(new DllNotFoundException(), false).Should().BeNull();

    [Fact]
    public void Another_exception_on_Linux_has_no_special_message() =>
        StartupFailure.MissingNativeLibraryMessage(new InvalidOperationException(), true).Should().BeNull();
}
