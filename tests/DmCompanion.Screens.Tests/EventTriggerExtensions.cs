using AngleSharp;
using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace DmCompanion.Screens.Tests;

/// <summary>
/// Synchronous event triggers that do not return until the handler has run.
/// <para>
/// bUnit's own <c>Click()</c>, <c>Input()</c>... are fire-and-forget (<c>TriggerEvent</c> discards the task of
/// <c>Dispatcher.InvokeAsync</c>). When the test thread finds the renderer's dispatcher free it runs the handler inline, but when
/// a pool thread is inside the dispatcher (a repaint from a file watcher or the audio library) the handler is queued behind it and
/// runs later, on that thread: the test goes on reading state the event has not changed yet, or an element that a later render has
/// already replaced. These methods live in the tests' own namespace, so they take precedence over bUnit's extension methods of the
/// same name for every test in this project, and they add a barrier on the dispatcher after the event: the dispatcher runs its work
/// in order, so when the barrier runs the handler's synchronous part and the render it caused are done.
/// </para>
/// <para>The barrier does not wait for the asynchronous tail of a handler (it may be waiting on something the test releases later).</para>
/// <para>
/// Covered triggers: <c>Click</c>, <c>Input</c> (generic value and <see cref="ChangeEventArgs"/>), <c>Change</c> (same),
/// <c>KeyDown</c> (<see cref="KeyboardEventArgs"/> and <see cref="Key"/>) and <c>Blur</c>. When a test needs another synchronous
/// trigger (DoubleClick, KeyUp, Submit, ...), add its wrapper here or use the *Async variant.
/// </para>
/// </summary>
internal static class EventTriggerExtensions
{
    public static void Click(this IElement element, MouseEventArgs? eventArgs = null)
    {
        var dispatcher = DispatcherOf(element);
        EventHandlerDispatchExtensions.Click(element, eventArgs ?? new MouseEventArgs());
        Settle(dispatcher);
    }

    public static void Input<T>(this IElement element, T value)
    {
        var dispatcher = DispatcherOf(element);
        EventHandlerDispatchExtensions.Input(element, value);
        Settle(dispatcher);
    }

    public static void Change<T>(this IElement element, T value)
    {
        var dispatcher = DispatcherOf(element);
        EventHandlerDispatchExtensions.Change(element, value);
        Settle(dispatcher);
    }

    public static void Input(this IElement element, ChangeEventArgs eventArgs)
    {
        var dispatcher = DispatcherOf(element);
        EventHandlerDispatchExtensions.Input(element, eventArgs);
        Settle(dispatcher);
    }

    public static void Change(this IElement element, ChangeEventArgs eventArgs)
    {
        var dispatcher = DispatcherOf(element);
        EventHandlerDispatchExtensions.Change(element, eventArgs);
        Settle(dispatcher);
    }

    public static void KeyDown(this IElement element, KeyboardEventArgs eventArgs)
    {
        var dispatcher = DispatcherOf(element);
        EventHandlerDispatchExtensions.KeyDown(element, eventArgs);
        Settle(dispatcher);
    }

    public static void KeyDown(this IElement element, Key key, bool repeat = default, string? type = default)
    {
        var dispatcher = DispatcherOf(element);
        EventHandlerDispatchExtensions.KeyDown(element, key, repeat, type);
        Settle(dispatcher);
    }

    public static void Blur(this IElement element, FocusEventArgs? eventArgs = null)
    {
        var dispatcher = DispatcherOf(element);
        EventHandlerDispatchExtensions.Blur(element, eventArgs ?? new FocusEventArgs());
        Settle(dispatcher);
    }

    // The element may remove itself from the DOM when handling the event, so the dispatcher is taken before raising it.
    private static Dispatcher DispatcherOf(IElement element) =>
        (element.Owner?.Context.GetService<BunitContext>()
            ?? throw new InvalidOperationException("The element was not rendered by a bUnit context.")).Renderer.Dispatcher;

    private static void Settle(Dispatcher dispatcher)
    {
        var barrier = dispatcher.InvokeAsync(() => { });
        try
        {
            if (!barrier.Wait(BunitContext.DefaultWaitTimeout))
                throw new TimeoutException("The renderer dispatcher did not become free after the event.");
        }
        catch (AggregateException ex) when (ex.InnerExceptions.Count == 1)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerExceptions[0]).Throw();
        }
    }
}
