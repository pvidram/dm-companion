using Microsoft.Extensions.Time.Testing;

namespace DmCompanion.Screens.Tests;

// Reloj falso cuyos temporizadores ejecutan una acción, una sola vez, justo antes del siguiente Change: permite colar otra
// operación dentro de StopTimer. Tras Dispose, Change lanza ObjectDisposedException, como documenta Timer.Change
// (los temporizadores de TimeProvider.System y FakeTimeProvider de .NET 10 devuelven false en su lugar).
internal sealed class HookedTimeProvider : TimeProvider
{
    private readonly FakeTimeProvider _inner = new();
    public Action? BeforeNextChange;

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) =>
        new HookedTimer(_inner.CreateTimer(callback, state, dueTime, period), this);

    private sealed class HookedTimer(ITimer inner, HookedTimeProvider owner) : ITimer
    {
        private volatile bool _disposed;

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            Interlocked.Exchange(ref owner.BeforeNextChange, null)?.Invoke();
            ObjectDisposedException.ThrowIf(_disposed, this);
            return inner.Change(dueTime, period);
        }

        public void Dispose()
        {
            _disposed = true;
            inner.Dispose();
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
