using DmCompanion.UI.Interop;
using Microsoft.AspNetCore.Components;

namespace DmCompanion.UI.Tests.Support;

internal sealed class FakeFocusInterop : IFocusInterop
{
    public List<string> Calls { get; } = [];

    public ValueTask EnterAsync(ElementReference container, bool trap)
    {
        Calls.Add(trap ? "enter:trap" : "enter");
        return ValueTask.CompletedTask;
    }

    public ValueTask ExitAsync(ElementReference container)
    {
        Calls.Add("exit");
        return ValueTask.CompletedTask;
    }
}
