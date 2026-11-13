using Microsoft.Extensions.Configuration;

namespace DmCompanion.Desktop.Tests;

public sealed class StartupFailureTests : IDisposable
{
    private readonly TempVault _folder = TempVault.Empty();

    public void Dispose() => _folder.Dispose();

    [Fact]
    public void Report_writes_the_message_to_the_console()
    {
        var output = new StringWriter();

        StartupFailure.Report("No se pudo arrancar.", null, null, wait: false, output, new StringReader(""));

        output.ToString().Should().Contain("No se pudo arrancar.");
    }

    [Fact]
    public void Report_returns_a_non_zero_exit_code() =>
        StartupFailure.Report("x", null, null, wait: false, new StringWriter(), new StringReader("")).Should().NotBe(0);

    [Fact]
    public void Report_waits_for_enter_when_asked()
    {
        var input = new StringReader("primera\nsegunda\n");

        StartupFailure.Report("x", null, null, wait: true, new StringWriter(), input);

        input.ReadLine().Should().Be("segunda");
    }

    [Fact]
    public void Report_does_not_read_the_input_when_not_waiting()
    {
        var input = new StringReader("primera\n");

        StartupFailure.Report("x", null, null, wait: false, new StringWriter(), input);

        input.ReadLine().Should().Be("primera");
    }

    [Fact]
    public void Report_logs_only_the_exception_type()
    {
        using var provider = new Logging.DailyFileLoggerProvider(_folder.Root, TimeProvider.System);

        StartupFailure.Report("x", provider.CreateLogger("DmCompanion.Desktop"), new IOException("C:\\secreto"), wait: false, new StringWriter(), new StringReader(""));

        string.Concat(Directory.GetFiles(_folder.Root).Select(File.ReadAllText)).Should().Contain("[IOException]").And.NotContain("secreto");
    }

    [Fact]
    public void A_failure_after_startup_does_not_say_it_could_not_start() =>
        StartupFailure.UnexpectedFailureMessage(hasStarted: true).Should().Contain("se ha detenido por un error").And.NotContain("no ha podido arrancar");

    [Fact]
    public void A_failure_before_startup_says_it_could_not_start() =>
        StartupFailure.UnexpectedFailureMessage(hasStarted: false).Should().Contain("no ha podido arrancar");

    [Fact]
    public void ShouldWait_can_be_disabled_by_configuration()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Desktop:WaitOnError"] = "false" }).Build();

        StartupFailure.ShouldWait(config).Should().BeFalse();
    }

    [Fact]
    public void ShouldWait_can_be_forced_by_configuration()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Desktop:WaitOnError"] = "true" }).Build();

        StartupFailure.ShouldWait(config).Should().BeTrue();
    }

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
}
