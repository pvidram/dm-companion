using DmCompanion.UI.Interop;
using Microsoft.Extensions.DependencyInjection;

namespace DmCompanion.UI.Tests.Support;

public abstract class UiTestContext : BunitContext
{
    protected UiTestContext() => Services.AddSingleton<IFocusInterop>(Focus);

    internal FakeFocusInterop Focus { get; } = new();
}
