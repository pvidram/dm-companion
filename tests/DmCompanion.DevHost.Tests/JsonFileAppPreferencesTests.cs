using DmCompanion.DevHost.Platform;
using Microsoft.Extensions.Logging;

namespace DmCompanion.DevHost.Tests;

public sealed class JsonFileAppPreferencesTests : IDisposable
{
    private readonly TempVault _folder = TempVault.Empty();
    private string FilePath => _folder.PathOf("prefs.json");

    public void Dispose() => _folder.Dispose();

    [Fact]
    public void Set_then_Get_round_trips_through_the_file()
    {
        new JsonFileAppPreferences(FilePath).Set("k", "v");

        new JsonFileAppPreferences(FilePath).Get("k").Should().Be("v");
    }

    [Fact]
    public void A_corrupt_file_is_treated_as_empty()
    {
        File.WriteAllText(FilePath, "{ esto no es json");

        new JsonFileAppPreferences(FilePath).Get("k").Should().BeNull();
    }

    [Fact]
    public void A_corrupt_file_logs_a_warning()
    {
        File.WriteAllText(FilePath, "{ esto no es json");
        var logger = new WarningCounter();

        new JsonFileAppPreferences(FilePath, logger).Get("k");

        logger.Warnings.Should().Be(1);
    }

    [Fact]
    public void A_corrupt_file_is_overwritten_by_the_next_Set()
    {
        File.WriteAllText(FilePath, "{ esto no es json");
        new JsonFileAppPreferences(FilePath).Set("k", "v");

        new JsonFileAppPreferences(FilePath).Get("k").Should().Be("v");
    }

    private sealed class WarningCounter : ILogger<JsonFileAppPreferences>
    {
        public int Warnings { get; private set; }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning) Warnings++;
        }
    }
}
