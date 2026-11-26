using System.Globalization;
using DmCompanion.UI.Components;
using DmCompanion.UI.Interop;
using DmCompanion.UI.Tests.Support;
using Microsoft.AspNetCore.Components.Web;

namespace DmCompanion.UI.Tests;

public class OverlayTests : UiTestContext
{
    private IRenderedComponent<Parchment> RenderParchment(bool isOpen, Action? onClose = null) =>
        Render<Parchment>(p => p
            .Add(x => x.Title, "Notas")
            .Add(x => x.IsOpen, isOpen)
            .Add(x => x.OnClose, onClose ?? (() => { }))
            .AddChildContent("<p>Cuerpo</p>"));

    [Fact]
    public void Escape_closes_the_parchment()
    {
        var closed = false;
        var cut = RenderParchment(isOpen: true, () => closed = true);
        cut.Find(".ds-parchment").KeyDown(new KeyboardEventArgs { Key = "Escape" });
        closed.Should().BeTrue();
    }

    [Fact]
    public void Close_button_closes_the_parchment()
    {
        var closed = false;
        var cut = RenderParchment(isOpen: true, () => closed = true);
        cut.Find("button[aria-label='Cerrar']").Click();
        closed.Should().BeTrue();
    }

    [Fact]
    public void Closed_parchment_stays_mounted_but_hidden()
    {
        var cut = RenderParchment(isOpen: false);
        cut.Find(".ds-parchment").HasAttribute("hidden").Should().BeTrue();
    }

    [Fact]
    public void Opening_the_parchment_moves_focus_in_without_trapping_it()
    {
        RenderParchment(isOpen: true);
        Focus.Calls.Should().Equal("enter");
    }

    [Fact]
    public void Closing_the_parchment_returns_the_focus()
    {
        var cut = RenderParchment(isOpen: true);
        cut.Render(p => p.Add(x => x.IsOpen, false));
        Focus.Calls.Should().Equal("enter", "exit");
    }

    [Fact]
    public async Task Disposing_an_open_parchment_returns_the_focus()
    {
        RenderParchment(isOpen: true);
        await DisposeComponentsAsync();
        Focus.Calls.Should().Equal("enter", "exit");
    }

    [Fact]
    public void Dialog_is_closed_by_default()
    {
        var cut = Render<Dialog>(p => p.Add(x => x.Title, "Aviso"));
        cut.Find(".ds-dialog-backdrop").HasAttribute("hidden").Should().BeTrue();
    }

    [Fact]
    public void Parchment_renders_its_actions_slot()
    {
        var cut = Render<Parchment>(p => p
            .Add(x => x.Title, "Ficha")
            .Add(x => x.Actions, "<button>Mostrar en TV</button>"));
        cut.Find(".ds-parchment__actions").TextContent.Should().Contain("Mostrar en TV");
    }

    [Fact]
    public void Quick_card_position_uses_invariant_numbers_under_a_spanish_culture()
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("es-ES");
        try
        {
            var cut = Render<QuickCard>(p => p.Add(x => x.Title, "Goblin").Add(x => x.X, 12.5).Add(x => x.Y, 40));
            cut.Find(".ds-quick-card").GetAttribute("style").Should().Contain("left: 12.5px");
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void Draggable_quick_card_marks_its_header_as_the_handle()
    {
        var cut = Render<QuickCard>(p => p.Add(x => x.Title, "Goblin").Add(x => x.Draggable, true));
        cut.Find(".ds-parchment__header").HasAttribute("data-drag-handle").Should().BeTrue();
    }

    [Fact]
    public void Quick_card_is_not_draggable_by_default()
    {
        var cut = Render<QuickCard>(p => p.Add(x => x.Title, "Goblin"));
        cut.Find(".ds-parchment__header").HasAttribute("data-drag-handle").Should().BeFalse();
    }

    [Fact]
    public void Escape_closes_the_quick_card()
    {
        var closed = false;
        var cut = Render<QuickCard>(p => p.Add(x => x.Title, "Goblin").Add(x => x.OnClose, () => closed = true));
        cut.Find(".ds-quick-card").KeyDown(new KeyboardEventArgs { Key = "Escape" });
        closed.Should().BeTrue();
    }

    [Fact]
    public void Enemy_quick_card_shows_the_swords_icon()
    {
        var cut = Render<QuickCard>(p => p.Add(x => x.Title, "Goblin").Add(x => x.Faction, Faction.Enemy));
        cut.Find(".ds-quick-card .ds-icon").GetAttribute("style").Should().Contain("swords.svg");
    }

    [Fact]
    public void Parchment_is_labelled_by_its_title_element()
    {
        var cut = RenderParchment(isOpen: true);
        var labelledBy = cut.Find(".ds-parchment").GetAttribute("aria-labelledby");
        cut.Find(".ds-parchment__title").Id.Should().Be(labelledBy);
    }

    [Fact]
    public void Quick_card_is_labelled_by_its_title_element()
    {
        var cut = Render<QuickCard>(p => p.Add(x => x.Title, "Goblin"));
        var labelledBy = cut.Find(".ds-quick-card").GetAttribute("aria-labelledby");
        cut.Find(".ds-parchment__title").Id.Should().Be(labelledBy);
    }

    [Fact]
    public void Resizable_parchment_has_three_handles()
    {
        var cut = Render<Parchment>(p => p.Add(x => x.Title, "Notas").Add(x => x.Resizable, true));
        cut.FindAll("[data-resize-handle]").Select(h => h.GetAttribute("data-resize-handle")).Should().Equal("e", "s", "se");
    }

    [Fact]
    public void Full_parchment_is_never_resizable()
    {
        var cut = Render<Parchment>(p => p.Add(x => x.Title, "Notas").Add(x => x.Resizable, true).Add(x => x.Size, ParchmentSize.Full));
        cut.FindAll("[data-resize-handle]").Should().BeEmpty();
    }

    [Fact]
    public void Resizable_parchment_attaches_with_side_edges()
    {
        Render<Parchment>(p => p.Add(x => x.Title, "Notas").Add(x => x.Resizable, true));
        Frames.Attached.Should().ContainSingle().Which.Edges.Should().Be(ResizeEdges.Sides);
    }

    [Fact]
    public void Quick_card_without_pin_callback_has_no_pin_button()
    {
        var cut = Render<QuickCard>(p => p.Add(x => x.Title, "Goblin"));
        cut.FindAll("button[aria-label='Anclar']").Should().BeEmpty();
    }

    [Fact]
    public void Quick_card_header_actions_render_before_the_pin()
    {
        var cut = Render<QuickCard>(p => p
            .Add(x => x.Title, "Goblin")
            .Add(x => x.OnPinnedChanged, (bool _) => { })
            .Add(x => x.HeaderActions, "<button id=\"extra\">x</button>"));
        cut.Find("header").InnerHtml.IndexOf("id=\"extra\"", StringComparison.Ordinal)
            .Should().BeLessThan(cut.Find("header").InnerHtml.IndexOf("Anclar", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Frame_change_from_js_raises_OnFrameChanged()
    {
        WindowFrameChange? received = null;
        Render<QuickCard>(p => p.Add(x => x.Title, "Goblin").Add(x => x.Draggable, true)
            .Add(x => x.OnFrameChanged, (WindowFrameChange c) => received = c));
        var change = new WindowFrameChange(10, 20, new WindowSize(300, 200), null);
        await Frames.Listener!.OnFrameChangedAsync(change);
        received.Should().Be(change);
    }

    [Fact]
    public void Quick_card_renders_only_the_chosen_axes_in_its_style()
    {
        var cut = Render<QuickCard>(p => p.Add(x => x.Title, "Goblin").Add(x => x.X, 5).Add(x => x.Y, 6).Add(x => x.FrameSize, new WindowSize(420, null)));
        cut.Find("section").GetAttribute("style").Should().Contain("width: 420px").And.NotContain("height");
    }

    [Fact]
    public void Quick_card_without_position_renders_no_style_even_with_a_frame_size()
    {
        var cut = Render<QuickCard>(p => p.Add(x => x.Title, "Goblin").Add(x => x.FrameSize, new WindowSize(420, 300)));
        cut.Find("section").HasAttribute("style").Should().BeFalse();
    }

    [Fact]
    public void Quick_card_layer_is_clamped_below_the_toast()
    {
        var cut = Render<QuickCard>(p => p.Add(x => x.Title, "Goblin").Add(x => x.Layer, 9));
        cut.Find("section").GetAttribute("style").Should().Contain("+ 4)");
    }

    [Fact]
    public void Pinned_quick_card_has_the_pinned_modifier()
    {
        var cut = Render<QuickCard>(p => p.Add(x => x.Title, "Goblin").Add(x => x.IsPinned, true));
        cut.Find("section").ClassList.Should().Contain("ds-quick-card--pinned");
    }

    [Fact]
    public async Task A_move_only_change_has_no_size()
    {
        WindowFrameChange? received = null;
        Render<QuickCard>(p => p.Add(x => x.Title, "Goblin").Add(x => x.Draggable, true)
            .Add(x => x.OnFrameChanged, (WindowFrameChange c) => received = c));
        await Frames.Listener!.OnFrameChangedAsync(new WindowFrameChange(10, 20, null, null));
        received!.Size.Should().BeNull();
    }

    [Fact]
    public async Task Frame_reset_raises_OnFrameReset()
    {
        var reset = false;
        Render<QuickCard>(p => p.Add(x => x.Title, "Goblin").Add(x => x.Draggable, true).Add(x => x.OnFrameReset, () => reset = true));
        await Frames.Listener!.OnFrameResetAsync();
        reset.Should().BeTrue();
    }

    [Fact]
    public void Quick_card_updates_the_frame_when_its_position_changes()
    {
        var cut = Render<QuickCard>(p => p.Add(x => x.Title, "Goblin").Add(x => x.Draggable, true).Add(x => x.X, 1).Add(x => x.Y, 2));
        cut.Render(p => p.Add(x => x.X, 50));
        Frames.Updated.Should().ContainSingle().Which.X.Should().Be(50);
    }

    [Fact]
    public void Quick_card_reapplies_the_frame_when_its_layer_changes()
    {
        var cut = Render<QuickCard>(p => p.Add(x => x.Title, "Goblin").Add(x => x.Draggable, true).Add(x => x.Layer, 1));
        cut.Render(p => p.Add(x => x.Layer, 2));
        Frames.Updated.Should().HaveCount(1);
    }
}
