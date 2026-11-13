using DmCompanion.UI.Components;
using DmCompanion.UI.Tests.Support;
using Microsoft.AspNetCore.Components.Web;

namespace DmCompanion.UI.Tests;

public class ToastTests : UiTestContext
{
    private static readonly ToastMessage Removed = new("Goblin quitado del mapa", "Deshacer", ActionIcon: IconName.Undo2);

    [Fact]
    public void Region_is_a_polite_live_region_even_without_message()
    {
        var cut = Render<Toast>();
        cut.Find(".ds-toast-region[role='status']").GetAttribute("aria-live").Should().Be("polite");
    }

    [Fact]
    public void Toast_shows_the_message_text()
    {
        var cut = Render<Toast>(p => p.Add(x => x.Message, Removed));
        cut.Find(".ds-toast__text").TextContent.Should().Be("Goblin quitado del mapa");
    }

    [Fact]
    public void Action_button_emits_on_action_with_the_message()
    {
        ToastMessage? acted = null;
        var cut = Render<Toast>(p => p.Add(x => x.Message, Removed).Add(x => x.OnAction, (ToastMessage m) => acted = m));
        cut.Find(".ds-toast .ds-button").Click();
        acted.Should().BeSameAs(Removed);
    }

    [Fact]
    public void Action_hides_the_toast()
    {
        var cut = Render<Toast>(p => p.Add(x => x.Message, Removed));
        cut.Find(".ds-toast .ds-button").Click();
        cut.FindAll(".ds-toast").Should().BeEmpty();
    }

    [Fact]
    public void Message_without_action_has_no_action_button()
    {
        var cut = Render<Toast>(p => p.Add(x => x.Message, new ToastMessage("2 añadidos, 3 ya estaban")));
        cut.FindAll(".ds-toast .ds-button").Should().BeEmpty();
    }

    [Fact]
    public void Close_button_emits_on_dismissed()
    {
        ToastMessage? dismissed = null;
        var cut = Render<Toast>(p => p.Add(x => x.Message, Removed).Add(x => x.OnDismissed, (ToastMessage m) => dismissed = m));
        cut.Find("button[aria-label='Cerrar aviso']").Click();
        dismissed.Should().BeSameAs(Removed);
    }

    [Fact]
    public void Toast_expires_after_its_duration()
    {
        ToastMessage? dismissed = null;
        var cut = Render<Toast>(p => p.Add(x => x.Message, Removed).Add(x => x.OnDismissed, (ToastMessage m) => dismissed = m));
        Time.Advance(Toast.DefaultDuration);
        cut.WaitForAssertion(() => dismissed.Should().BeSameAs(Removed));
    }

    [Fact]
    public void Toast_honours_a_custom_duration()
    {
        var message = new ToastMessage("Guardado", Duration: TimeSpan.FromSeconds(2));
        var cut = Render<Toast>(p => p.Add(x => x.Message, message));
        Time.Advance(TimeSpan.FromSeconds(2));
        cut.WaitForAssertion(() => cut.FindAll(".ds-toast").Should().BeEmpty());
    }

    [Fact]
    public void Toast_does_not_expire_while_the_pointer_is_over_it()
    {
        var cut = Render<Toast>(p => p.Add(x => x.Message, Removed));
        cut.Find(".ds-toast").PointerEnter(new PointerEventArgs());
        Time.Advance(TimeSpan.FromSeconds(30));
        cut.FindAll(".ds-toast").Should().HaveCount(1);
    }

    [Fact]
    public void Toast_does_not_expire_while_it_has_focus()
    {
        var cut = Render<Toast>(p => p.Add(x => x.Message, Removed));
        cut.Find(".ds-toast").FocusIn(new FocusEventArgs());
        Time.Advance(TimeSpan.FromSeconds(30));
        cut.FindAll(".ds-toast").Should().HaveCount(1);
    }

    [Fact]
    public void A_new_message_with_the_same_text_restarts_the_timer()
    {
        var cut = Render<Toast>(p => p.Add(x => x.Message, new ToastMessage("Goblin quitado del mapa")));
        Time.Advance(TimeSpan.FromSeconds(4));
        cut.Render(p => p.Add(x => x.Message, new ToastMessage("Goblin quitado del mapa")));
        Time.Advance(TimeSpan.FromSeconds(4));
        cut.FindAll(".ds-toast").Should().HaveCount(1);
    }

    [Fact]
    public void A_replacement_message_expires_after_a_full_duration_from_its_arrival()
    {
        var cut = Render<Toast>(p => p.Add(x => x.Message, new ToastMessage("Goblin quitado del mapa")));
        Time.Advance(TimeSpan.FromSeconds(4));
        cut.Render(p => p.Add(x => x.Message, new ToastMessage("Goblin quitado del mapa")));
        Time.Advance(Toast.DefaultDuration);
        cut.WaitForAssertion(() => cut.FindAll(".ds-toast").Should().BeEmpty());
    }

    [Fact]
    public void Toast_expires_after_the_pointer_leaves()
    {
        var cut = Render<Toast>(p => p.Add(x => x.Message, Removed));
        cut.Find(".ds-toast").PointerEnter(new PointerEventArgs());
        cut.Find(".ds-toast").PointerLeave(new PointerEventArgs());
        Time.Advance(Toast.DefaultDuration);
        cut.WaitForAssertion(() => cut.FindAll(".ds-toast").Should().BeEmpty());
    }

    [Fact]
    public void Toast_does_not_expire_when_the_pointer_leaves_but_focus_remains()
    {
        var cut = Render<Toast>(p => p.Add(x => x.Message, Removed));
        cut.Find(".ds-toast").FocusIn(new FocusEventArgs());
        cut.Find(".ds-toast").PointerEnter(new PointerEventArgs());
        cut.Find(".ds-toast").PointerLeave(new PointerEventArgs());
        Time.Advance(TimeSpan.FromSeconds(30));
        cut.FindAll(".ds-toast").Should().HaveCount(1);
    }

    [Fact]
    public void Replacing_a_visible_message_dismisses_the_previous_one()
    {
        ToastMessage? dismissed = null;
        var first = new ToastMessage("Primero");
        var cut = Render<Toast>(p => p.Add(x => x.Message, first).Add(x => x.OnDismissed, (ToastMessage m) => dismissed = m));
        cut.Render(p => p.Add(x => x.Message, new ToastMessage("Segundo")));
        dismissed.Should().BeSameAs(first);
    }

    [Fact]
    public void Action_does_not_emit_on_dismissed()
    {
        var dismissals = 0;
        var cut = Render<Toast>(p => p.Add(x => x.Message, Removed).Add(x => x.OnDismissed, (ToastMessage _) => dismissals++));
        cut.Find(".ds-toast .ds-button").Click();
        dismissals.Should().Be(0);
    }

    [Fact]
    public void Clearing_the_message_resets_the_pause_flags_for_the_next_one()
    {
        var cut = Render<Toast>(p => p.Add(x => x.Message, Removed));
        cut.Find(".ds-toast").FocusIn(new FocusEventArgs());
        cut.Render(p => p.Add(x => x.Message, (ToastMessage?)null));
        cut.Render(p => p.Add(x => x.Message, new ToastMessage("Otro")));
        Time.Advance(Toast.DefaultDuration);
        cut.WaitForAssertion(() => cut.FindAll(".ds-toast").Should().BeEmpty());
    }
}
