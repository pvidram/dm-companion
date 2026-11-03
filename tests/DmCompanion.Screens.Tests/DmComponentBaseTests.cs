using Bunit;
using DmCompanion.Screens.Components;
using DmCompanion.Screens.Platform;
using DmCompanion.Screens.Services;
using DmCompanion.Infrastructure;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DmCompanion.Screens.Tests;

public sealed class DmComponentBaseTests : BunitContext, IAsyncLifetime
{
    Task IAsyncLifetime.InitializeAsync() => Task.CompletedTask;

    async Task IAsyncLifetime.DisposeAsync() => await base.DisposeAsync();

    private readonly CapturingLogger _logger = new();

    public DmComponentBaseTests()
    {
        Services.AddSingleton<IAppPreferences, InMemoryAppPreferences>();
        Services.AddSingleton(new FileSystemVaultServiceFactory(NullLoggerFactory.Instance));
        Services.AddSingleton<DmCompanion.Core.Vault.Storage.IVaultServiceFactory>(sp => sp.GetRequiredService<FileSystemVaultServiceFactory>());
        Services.AddSingleton<VaultHost>();
        Services.AddScoped<ShellState>();
        Services.AddSingleton<ILogger<DmComponentBase>>(_logger);
    }

    [Fact]
    public void A_failure_in_OnVaultChanged_is_logged_as_an_error()
    {
        Render<ThrowingComponent>();

        Services.GetRequiredService<ShellState>().Toggle(OverlayPanel.Players);

        _logger.Errors.Should().ContainSingle(e => e.Message.Contains(nameof(InvalidOperationException)));
    }

    private sealed class ThrowingComponent : DmComponentBase
    {
        protected override void OnVaultChanged() => throw new InvalidOperationException("boom");
    }

    private sealed class CapturingLogger : ILogger<DmComponentBase>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];
        public IEnumerable<(LogLevel Level, string Message)> Errors => Entries.Where(e => e.Level == LogLevel.Error);

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));
    }
}
