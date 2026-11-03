using DmCompanion.UI.Components;
using DmCompanion.UI.Tests.Support;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace DmCompanion.UI.Tests;

public class DialogTests : UiTestContext
{
    private IRenderedComponent<ConfirmDialog> RenderConfirm(bool isOpen, Action? onConfirm = null, Action? onCancel = null) =>
        Render<ConfirmDialog>(p => p
            .Add(x => x.Title, "Conflicto")
            .Add(x => x.Message, "La nota ha cambiado en disco.")
            .Add(x => x.IsOpen, isOpen)
            .Add(x => x.OnConfirm, onConfirm ?? (() => { }))
            .Add(x => x.OnCancel, onCancel ?? (() => { })));

    [Fact]
    public void Opening_a_dialog_traps_the_focus()
    {
        RenderConfirm(isOpen: true);
        Focus.Calls.Should().Equal("enter:trap");
    }

    [Fact]
    public void Closing_a_dialog_returns_the_focus()
    {
        var cut = RenderConfirm(isOpen: true);
        cut.Render(p => p.Add(x => x.IsOpen, false));
        Focus.Calls.Should().Equal("enter:trap", "exit");
    }

    [Fact]
    public void Dialog_is_announced_as_modal()
    {
        var cut = RenderConfirm(isOpen: true);
        cut.Find("[role='dialog']").GetAttribute("aria-modal").Should().Be("true");
    }

    [Fact]
    public void Confirm_button_invokes_on_confirm()
    {
        var confirmed = false;
        var cut = RenderConfirm(isOpen: true, onConfirm: () => confirmed = true);
        cut.FindAll("button").Single(b => b.TextContent.Contains("Aceptar")).Click();
        confirmed.Should().BeTrue();
    }

    [Fact]
    public void Escape_cancels_the_confirm_dialog()
    {
        var cancelled = false;
        var cut = RenderConfirm(isOpen: true, onCancel: () => cancelled = true);
        cut.Find("[role='dialog']").KeyDown(new KeyboardEventArgs { Key = "Escape" });
        cancelled.Should().BeTrue();
    }

    private IRenderedComponent<Parchment> RenderParchmentWithNestedConfirm(Action onParchmentClose, Action onCancel) =>
        Render<Parchment>(p => p
            .Add(x => x.Title, "Notas")
            .Add(x => x.OnClose, onParchmentClose)
            .Add(x => x.ChildContent, builder =>
            {
                builder.OpenComponent<ConfirmDialog>(0);
                builder.AddAttribute(1, nameof(ConfirmDialog.Title), "Conflicto");
                builder.AddAttribute(2, nameof(ConfirmDialog.Message), "La nota ha cambiado en disco.");
                builder.AddAttribute(3, nameof(ConfirmDialog.IsOpen), true);
                builder.AddAttribute(4, nameof(ConfirmDialog.OnCancel),
                    EventCallback.Factory.Create(this, onCancel));
                builder.CloseComponent();
            }));

    [Fact]
    public void Escape_in_a_nested_dialog_does_not_close_the_parent_overlay()
    {
        var parchmentClosed = false;
        var cut = RenderParchmentWithNestedConfirm(() => parchmentClosed = true, () => { });
        cut.Find("[role='dialog']").KeyDown(new KeyboardEventArgs { Key = "Escape" });
        parchmentClosed.Should().BeFalse();
    }

    [Fact]
    public void Escape_in_a_nested_dialog_cancels_the_dialog()
    {
        var cancelled = false;
        var cut = RenderParchmentWithNestedConfirm(() => { }, () => cancelled = true);
        cut.Find("[role='dialog']").KeyDown(new KeyboardEventArgs { Key = "Escape" });
        cancelled.Should().BeTrue();
    }
}
