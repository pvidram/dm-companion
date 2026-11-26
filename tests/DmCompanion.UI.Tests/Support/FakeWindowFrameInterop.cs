using DmCompanion.UI.Interop;
using Microsoft.AspNetCore.Components;

namespace DmCompanion.UI.Tests.Support;

internal sealed class FakeWindowFrameInterop : IWindowFrameInterop
{
    public List<WindowFrameOptions> Attached { get; } = [];
    public List<WindowFrameOptions> Updated { get; } = [];
    public int Detached { get; private set; }
    public IWindowFrameListener? Listener { get; private set; }

    public ValueTask AttachAsync(ElementReference frame, WindowFrameOptions options, IWindowFrameListener listener)
    {
        Attached.Add(options);
        Listener = listener;
        return ValueTask.CompletedTask;
    }

    public ValueTask UpdateAsync(ElementReference frame, WindowFrameOptions options)
    {
        Updated.Add(options);
        return ValueTask.CompletedTask;
    }

    public ValueTask DetachAsync(ElementReference frame)
    {
        Detached++;
        return ValueTask.CompletedTask;
    }
}
