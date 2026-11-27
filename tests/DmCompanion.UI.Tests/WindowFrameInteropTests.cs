using DmCompanion.UI.Interop;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace DmCompanion.UI.Tests;

public class WindowFrameInteropTests : BunitContext
{
    [Fact]
    public async Task Detach_after_the_circuit_disconnects_logs_no_warning()
    {
        var module = JSInterop.SetupModule(WindowFrameInterop.ModulePath);
        module.SetupVoid("detach", _ => true).SetException(new JSDisconnectedException("cerrado"));
        var logger = new RecordingLogger();
        await using var interop = new WindowFrameInterop(JSInterop.JSRuntime, logger);

        await interop.DetachAsync(new ElementReference("frame"));

        logger.Warnings.Should().Be(0);
    }

    private sealed class RecordingLogger : ILogger<WindowFrameInterop>
    {
        public int Warnings { get; private set; }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel >= LogLevel.Warning) Warnings++;
        }
    }
}
