using Bunit;
using DmCompanion.Core.Combat;
using DmCompanion.Core.Maps;
using DmCompanion.Screens.Components;
using DmCompanion.Screens.Services;

namespace DmCompanion.Screens.Tests;

/// <summary>Marcar y revivir desde la fila, la tarjeta y el aviso de 3 fallos (spec §6.1).</summary>
public sealed class CombatDeadTests : CombatUiTestBase
{
    private static void OpenMenu(IRenderedComponent<DmShell> cut, int row) => cut.FindAll("[data-testid=strip-menu]")[row].Click();

    [Fact]
    public async Task Row_menu_marks_dead_and_revives()
    {
        var cut = await RenderAsync();

        OpenMenu(cut, 1);
        cut.Find("[data-testid=menu-dead]").Click();
        var marked = Tokens.First(t => t.Id == "b").IsDead;
        OpenMenu(cut, 1);
        var label = cut.Find("[data-testid=menu-dead]").TextContent.Trim();
        cut.Find("[data-testid=menu-dead]").Click();

        (marked, label, Tokens.First(t => t.Id == "b").IsDead).Should().Be((true, "Revivir", false));
    }

    [Fact]
    public async Task Row_menu_dead_is_disabled_in_read_only()
    {
        var cut = await RenderAsync();
        MakeReadOnly();
        cut.Find("[data-testid=strip-toggle]").Click();
        cut.Find("[data-testid=strip-toggle]").Click();

        cut.FindAll("[data-testid=strip-menu]")[1].HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public async Task Dead_row_is_struck_through()
    {
        var cut = await RenderAsync();
        await OnDispatcherAsync(() => Combat.MarkDead("b"));
        cut.FindAll("[data-testid=strip-row]")[1].ClassList.Should().Contain("combat-strip__row--dead");
    }

    [Fact]
    public async Task Alive_row_is_not_struck_through()
    {
        var cut = await RenderAsync();

        cut.FindAll("[data-testid=strip-row]")[1].ClassList.Should().NotContain("combat-strip__row--dead");
    }

    [Fact]
    public async Task Dead_cell_is_greyed_in_the_collapsed_strip()
    {
        var cut = await RenderAsync();
        await OnDispatcherAsync(() => Combat.MarkDead("b"));
        cut.Find("[data-testid=strip-toggle]").Click();

        cut.FindAll("[data-testid=strip-cell]")[1].ClassList.Should().Contain("combat-strip__cell--dead");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Card_marks_dead_with_and_without_combat(bool combat)
    {
        await RenderAsync(start: combat);
        Shell.OpenTokenCard(new TokenCardRequest("a", 100, 100));
        var card = Render<TokenCard>();

        card.Find("[data-testid=token-dead]").Click();

        (Ana.IsDead, card.Find("[data-testid=token-dead]").TextContent.Trim()).Should().Be((true, "Revivir"));
    }

    [Fact]
    public async Task Card_revives()
    {
        await RenderAsync(start: false);
        await OnDispatcherAsync(() => Combat.MarkDead("a"));
        Shell.OpenTokenCard(new TokenCardRequest("a", 100, 100));
        var card = Render<TokenCard>();

        card.Find("[data-testid=token-dead]").Click();

        Ana.IsDead.Should().BeFalse();
    }

    [Fact]
    public async Task Card_dead_button_is_disabled_in_read_only()
    {
        await RenderAsync(start: false);
        MakeReadOnly();
        Shell.OpenTokenCard(new TokenCardRequest("a", 100, 100));
        var card = Render<TokenCard>();

        card.Find("[data-testid=token-dead]").HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public async Task Three_failures_notice_marks_dead()
    {
        var cut = await RenderAsync();
        await OnDispatcherAsync(() => Combat.SetDeathSaves("a", 0, 3));
        cut.Render();
        var text = cut.Find("[data-testid=notice-three-failures]").TextContent;

        cut.FindAll("[data-testid=notice-three-failures] button").First(b => b.TextContent.Trim() == "Marcar como muerto").Click();

        (text.Contains("Ana: 3 fallos. ¿Marcar como muerto?"), Ana.IsDead).Should().Be((true, true));
    }

    [Fact]
    public async Task Three_failures_notice_not_now_dismisses_without_marking()
    {
        var cut = await RenderAsync();
        await OnDispatcherAsync(() => Combat.SetDeathSaves("a", 0, 3));
        cut.Render();

        cut.FindAll("[data-testid=notice-three-failures] button").First(b => b.TextContent.Trim() == "Ahora no").Click();

        (cut.FindAll("[data-testid=notice-three-failures]").Count, Ana.IsDead).Should().Be((0, false));
    }

    [Fact]
    public async Task Three_failures_notice_survives_the_first_render()
    {
        var cut = await RenderAsync();
        await OnDispatcherAsync(() => Combat.SetDeathSaves("a", 0, 3));
        cut.Render();
        cut.Render();

        cut.FindAll("[data-testid=notice-three-failures]").Should().HaveCount(1);
    }
}
