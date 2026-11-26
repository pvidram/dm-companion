using Microsoft.AspNetCore.Components;

namespace DmCompanion.UI.Interop;

/// <summary>Mueve y redimensiona un marco con window-frame.js. El elemento debe contener los tiradores [data-drag-handle] y [data-resize-handle].</summary>
public interface IWindowFrameInterop
{
    ValueTask AttachAsync(ElementReference frame, WindowFrameOptions options, IWindowFrameListener listener);
    ValueTask UpdateAsync(ElementReference frame, WindowFrameOptions options);
    ValueTask DetachAsync(ElementReference frame);
}
