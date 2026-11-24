using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace DmCompanion.Screens.Tests;

/// <summary>
/// bUnit's synchronous <c>Click()</c>/<c>Input()</c>/... are fire-and-forget: they queue the handler on the renderer's dispatcher and
/// return. If a background thread (a file-watcher repaint, the audio library rebuild...) is inside the dispatcher at that moment, the
/// handler runs later, on that thread, and the test reads state that the event has not changed yet. With parallel collections the CPU
/// contention makes that window wide enough to hit. The triggers used by the tests (see <see cref="EventTriggerExtensions"/>)
/// must not return before the handler has run.
/// </summary>
public sealed class EventTriggerTests : BunitContext
{
    private int _clicks;
    private string? _typed;

    private IRenderedComponent<IComponent> RenderButtonAndInput() =>
        Render(builder =>
        {
            builder.OpenElement(0, "button");
            builder.AddAttribute(1, "onclick", EventCallback.Factory.Create<MouseEventArgs>(this, () => _clicks++));
            builder.CloseElement();
            builder.OpenElement(2, "input");
            builder.AddAttribute(3, "oninput", EventCallback.Factory.Create<ChangeEventArgs>(this, e => _typed = (string?)e.Value));
            builder.CloseElement();
        });

    /// <summary>Occupies the renderer's dispatcher from another thread and frees it after a short while.</summary>
    private void BusyDispatcherForAWhile()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = Task.Run(() => Renderer.Dispatcher.InvokeAsync(() =>
        {
            entered.SetResult();
            release.Task.Wait();
        }));
        entered.Task.Wait();
        _ = Task.Delay(150).ContinueWith(_ => release.SetResult(), TaskScheduler.Default);
    }

    [Fact]
    public void Click_has_run_the_handler_when_it_returns_even_if_another_thread_is_rendering()
    {
        var cut = RenderButtonAndInput();
        var button = cut.Find("button");
        BusyDispatcherForAWhile();

        button.Click();

        _clicks.Should().Be(1);
    }

    [Fact]
    public void Input_has_run_the_handler_when_it_returns_even_if_another_thread_is_rendering()
    {
        var cut = RenderButtonAndInput();
        var input = cut.Find("input");
        BusyDispatcherForAWhile();

        input.Input("hola");

        _typed.Should().Be("hola");
    }

    [Fact]
    public void Control_bUnits_own_Click_returns_before_the_handler_has_run_while_another_thread_is_rendering()
    {
        // Proves the premise: if a future bUnit waits natively, this fails and the wrappers can go.
        var cut = RenderButtonAndInput();
        var button = cut.Find("button");
        BusyDispatcherForAWhile();

        EventHandlerDispatchExtensions.Click(button, new MouseEventArgs());

        _clicks.Should().Be(0);
        cut.WaitForAssertion(() => _clicks.Should().Be(1));
    }
}
