using DmCompanion.Screens.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;

namespace DmCompanion.Screens.Tests;

public sealed class WikilinkInteropTests
{
    public static TheoryData<Exception> Failures => new()
    {
        new JSDisconnectedException("gone"),
        new JSException("js failed"),
        new TaskCanceledException(),
    };

    [Theory]
    [MemberData(nameof(Failures))]
    public async Task AttachAsync_swallows_interop_failures(Exception failure)
    {
        var interop = new WikilinkInterop(new ThrowingJs(failure), NullLogger<WikilinkInterop>.Instance);

        var act = async () => await interop.AttachAsync(default(ElementReference), DotNetObjectReference.Create(new object()));

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task AttachAsync_logs_a_JSException()
    {
        var logger = new ListLogger();
        var interop = new WikilinkInterop(new ThrowingJs(new JSException("js failed")), logger);

        await interop.AttachAsync(default(ElementReference), DotNetObjectReference.Create(new object()));

        logger.Levels.Should().Contain(LogLevel.Warning);
    }

    private sealed class ThrowingJs(Exception failure) : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => throw failure;
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => throw failure;
    }

    private sealed class ListLogger : ILogger<WikilinkInterop>
    {
        public List<LogLevel> Levels { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => Levels.Add(logLevel);
    }
}
