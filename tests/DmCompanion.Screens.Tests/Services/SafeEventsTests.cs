using DmCompanion.Screens.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace DmCompanion.Screens.Tests;

public class SafeEventsTests
{
    [Fact]
    public void Raise_calls_every_subscriber_even_if_one_throws()
    {
        var called = 0;
        EventHandler? handler = (_, _) => throw new InvalidOperationException();
        handler += (_, _) => called++;

        SafeEvents.Raise(handler, this, EventArgs.Empty, NullLogger.Instance, "Test");

        called.Should().Be(1);
    }

    [Fact]
    public void Raise_with_no_subscribers_does_nothing()
    {
        var act = () => SafeEvents.Raise(null, this, EventArgs.Empty, NullLogger.Instance, "Test");

        act.Should().NotThrow();
    }
}
