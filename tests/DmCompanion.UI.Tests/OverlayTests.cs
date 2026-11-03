using System.Globalization;
using DmCompanion.UI.Components;
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
}
