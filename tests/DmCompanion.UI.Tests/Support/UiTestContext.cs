using DmCompanion.UI.Interop;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace DmCompanion.UI.Tests.Support;

public abstract class UiTestContext : BunitContext
{
    protected UiTestContext()
    {
        Services.AddSingleton<IFocusInterop>(Focus);
        Services.AddSingleton<TimeProvider>(Time);
    }

    internal FakeFocusInterop Focus { get; } = new();

    /// <summary>Reloj controlado por el test (Advance) para los componentes con tiempo, como Toast.</summary>
    internal FakeTimeProvider Time { get; } = new();
}
