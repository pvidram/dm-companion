using DmCompanion.Desktop.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;

namespace DmCompanion.Desktop.Tests;

public sealed class DailyFileLoggerTests : IDisposable
{
    private readonly TempVault _folder = TempVault.Empty();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 13, 17, 4, 5, 123, TimeSpan.Zero));

    public DailyFileLoggerTests() => _time.SetLocalTimeZone(TimeZoneInfo.CreateCustomTimeZone("test", TimeSpan.FromHours(1), "test", "test"));

    public void Dispose() => _folder.Dispose();

    private ILogger CreateLogger(DailyFileLoggerProvider provider) => provider.CreateLogger("DmCompanion.Screens.Services.MapSession");

    private string ReadAll() => string.Concat(Directory.GetFiles(_folder.Root, "dm-companion-*.log").Order().Select(File.ReadAllText));

    [Fact]
    public void Writes_level_category_and_message_in_the_file_of_the_day()
    {
        using var provider = new DailyFileLoggerProvider(_folder.Root, _time);

        CreateLogger(provider).LogInformation("mensaje");

        File.ReadAllText(Path.Combine(_folder.Root, "dm-companion-20261113.log"))
            .Should().Be("2026-11-13T18:04:05.123+01:00 INFO  DmCompanion.Screens.Services.MapSession: mensaje" + Environment.NewLine);
    }

    [Fact]
    public void Writes_only_the_exception_type_between_brackets()
    {
        using var provider = new DailyFileLoggerProvider(_folder.Root, _time);

        CreateLogger(provider).LogError(new IOException("C:\\secreto\\ruta"), "fallo");

        ReadAll().Should().Contain("fallo [IOException]");
    }

    [Fact]
    public void Never_writes_the_exception_message()
    {
        using var provider = new DailyFileLoggerProvider(_folder.Root, _time);

        CreateLogger(provider).LogError(new IOException("C:\\secreto\\ruta"), "fallo");

        ReadAll().Should().NotContain("secreto");
    }

    [Fact]
    public void Writes_to_a_new_file_when_the_day_changes()
    {
        using var provider = new DailyFileLoggerProvider(_folder.Root, _time);
        var logger = CreateLogger(provider);
        logger.LogInformation("uno");

        _time.Advance(TimeSpan.FromDays(1));
        logger.LogInformation("dos");

        Directory.GetFiles(_folder.Root).Select(Path.GetFileName).Order()
            .Should().Equal("dm-companion-20261113.log", "dm-companion-20261114.log");
    }

    [Fact]
    public void Keeps_only_the_seven_most_recent_files_when_the_first_of_the_day_is_created()
    {
        for (var day = 1; day <= 9; day++)
            File.WriteAllText(Path.Combine(_folder.Root, $"dm-companion-202611{day:00}.log"), "x");
        using var provider = new DailyFileLoggerProvider(_folder.Root, _time);

        CreateLogger(provider).LogInformation("hoy");

        Directory.GetFiles(_folder.Root, "dm-companion-*.log").Should().HaveCount(7);
    }

    [Fact]
    public void Deletes_the_oldest_files_and_keeps_the_newest()
    {
        for (var day = 1; day <= 9; day++)
            File.WriteAllText(Path.Combine(_folder.Root, $"dm-companion-202611{day:00}.log"), "x");
        using var provider = new DailyFileLoggerProvider(_folder.Root, _time);

        CreateLogger(provider).LogInformation("hoy");

        File.Exists(Path.Combine(_folder.Root, "dm-companion-20261101.log")).Should().BeFalse();
    }

    [Fact]
    public void Does_not_write_below_information()
    {
        using var provider = new DailyFileLoggerProvider(_folder.Root, _time);

        CreateLogger(provider).LogDebug("detalle");

        Directory.GetFiles(_folder.Root).Should().BeEmpty();
    }

    [Fact]
    public void Does_not_throw_when_the_folder_cannot_be_written()
    {
        var blocker = Path.Combine(_folder.Root, "bloqueo");
        File.WriteAllText(blocker, "x");
        using var provider = new DailyFileLoggerProvider(Path.Combine(blocker, "logs"), _time);

        var act = () => CreateLogger(provider).LogInformation("mensaje");

        act.Should().NotThrow();
    }

    [Fact]
    public void A_framework_entry_with_an_exception_never_writes_its_formatted_message()
    {
        using var provider = new DailyFileLoggerProvider(_folder.Root, _time);

        provider.CreateLogger("Microsoft.AspNetCore.Components.Server.Circuits.CircuitHost")
            .LogWarning(new IOException("x"), "Unhandled exception rendering component: {Message}", "C:\\Users\\pau\\secreto");

        ReadAll().Should().NotContain("secreto");
    }

    [Fact]
    public void A_framework_entry_with_an_exception_writes_level_category_event_and_exception_type()
    {
        using var provider = new DailyFileLoggerProvider(_folder.Root, _time);

        provider.CreateLogger("Microsoft.AspNetCore.Components.Server.Circuits.CircuitHost")
            .Log(LogLevel.Warning, new EventId(111, "RenderFailed"), "texto {Message}", new IOException("x"), (s, _) => s?.ToString() ?? "");

        ReadAll().Should().Contain("WARN  Microsoft.AspNetCore.Components.Server.Circuits.CircuitHost: [111 RenderFailed] [IOException]");
    }

    [Fact]
    public void A_framework_entry_without_exception_keeps_its_message()
    {
        using var provider = new DailyFileLoggerProvider(_folder.Root, _time);

        provider.CreateLogger("Microsoft.Hosting.Lifetime").LogInformation("Application started.");

        ReadAll().Should().Contain("Application started.");
    }

    [Fact]
    public void An_app_entry_with_an_exception_keeps_its_own_message()
    {
        using var provider = new DailyFileLoggerProvider(_folder.Root, _time);

        CreateLogger(provider).LogError(new IOException("x"), "No se pudo guardar {Note}", "notas/a.md");

        ReadAll().Should().Contain("No se pudo guardar notas/a.md [IOException]");
    }
}
