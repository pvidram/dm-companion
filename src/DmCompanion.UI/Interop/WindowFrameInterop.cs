using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace DmCompanion.UI.Interop;

public sealed class WindowFrameInterop(IJSRuntime jsRuntime, ILogger<WindowFrameInterop> logger)
    : IWindowFrameInterop, IAsyncDisposable
{
    public const string ModulePath = "./_content/DmCompanion.UI/window-frame.js";
    public const double Margin = 16;

    private Task<IJSObjectReference>? _module;
    private readonly Dictionary<ElementReference, DotNetObjectReference<FrameCallbacks>> _references = [];

    // Si la importación falla, no se cachea el fallo: la siguiente llamada vuelve a intentarlo.
    private async Task<IJSObjectReference> LoadModuleAsync()
    {
        var pending = _module ??= jsRuntime.InvokeAsync<IJSObjectReference>("import", ModulePath).AsTask();
        try
        {
            return await pending;
        }
        catch
        {
            if (ReferenceEquals(_module, pending)) _module = null;
            throw;
        }
    }

    public async ValueTask AttachAsync(ElementReference frame, WindowFrameOptions options, IWindowFrameListener listener)
    {
        ReleaseReference(frame);
        var reference = DotNetObjectReference.Create(new FrameCallbacks(listener));
        _references[frame] = reference;
        if (!await TryInvokeAsync("attach", frame, ToJs(options), reference)) ReleaseReference(frame);
    }

    public async ValueTask UpdateAsync(ElementReference frame, WindowFrameOptions options) =>
        await TryInvokeAsync("update", frame, ToJs(options));

    public async ValueTask DetachAsync(ElementReference frame)
    {
        await TryInvokeAsync("detach", frame);
        ReleaseReference(frame);
    }

    private void ReleaseReference(ElementReference frame)
    {
        if (_references.Remove(frame, out var reference)) reference.Dispose();
    }

    private async Task<bool> TryInvokeAsync(string identifier, params object?[] args)
    {
        try
        {
            await (await LoadModuleAsync()).InvokeVoidAsync(identifier, args);
            return true;
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException or InvalidOperationException or TaskCanceledException)
        {
            logger.LogWarning(ex, "window-frame.js {Function} failed", identifier);
            return false;
        }
    }

    private static object ToJs(WindowFrameOptions o) => new
    {
        movable = o.Movable,
        edges = o.Edges switch { ResizeEdges.Corner => "corner", ResizeEdges.Sides => "sides", _ => "none" },
        minWidth = o.MinSize.Width,
        minHeight = o.MinSize.Height,
        margin = Margin,
        size = o.Size is null ? null : new { width = o.Size.Width, height = o.Size.Height },
        position = o.X is { } x && o.Y is { } y ? new { x, y } : null,
        anchor = o.Anchor is null ? null : new
        {
            corner = o.Anchor.Corner switch
            {
                WindowCorner.TopLeft => "tl",
                WindowCorner.TopRight => "tr",
                WindowCorner.BottomLeft => "bl",
                _ => "br",
            },
            dx = o.Anchor.OffsetX,
            dy = o.Anchor.OffsetY,
        },
    };

    public async ValueTask DisposeAsync()
    {
        foreach (var reference in _references.Values) reference.Dispose();
        _references.Clear();
        if (_module is null) return;
        try
        {
            await (await _module).DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
            // El circuito ya se cerró: el módulo JS desapareció con él.
        }
    }

    /// <summary>Callbacks que invoca window-frame.js; reenvían al listener.</summary>
    internal sealed class FrameCallbacks(IWindowFrameListener listener)
    {
        [JSInvokable]
        public Task OnFrameChanged(FrameChangeDto dto) => listener.OnFrameChangedAsync(new WindowFrameChange(
            dto.X, dto.Y, dto.Size is null ? null : new WindowSize(dto.Size.Width, dto.Size.Height), ToAnchor(dto.Anchor)));

        [JSInvokable]
        public Task OnFrameReset() => listener.OnFrameResetAsync();

        private static WindowAnchor? ToAnchor(AnchorDto? a) => a is null ? null : new WindowAnchor(
            a.Corner switch
            {
                "tl" => WindowCorner.TopLeft,
                "tr" => WindowCorner.TopRight,
                "bl" => WindowCorner.BottomLeft,
                _ => WindowCorner.BottomRight,
            }, a.Dx, a.Dy);
    }

    internal sealed record FrameChangeDto(double X, double Y, SizeDto? Size, AnchorDto? Anchor);

    internal sealed record SizeDto(double? Width, double? Height);

    internal sealed record AnchorDto(string Corner, double Dx, double Dy);
}
