using Bunit;
using DmCompanion.Core.Combat;
using DmCompanion.Core.Maps;
using DmCompanion.Screens.Components;
using DmCompanion.Screens.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DmCompanion.Screens.Tests;

public sealed class StartCombatDialogTests : TokenTestBase
{
    private CombatController Combat => Services.GetRequiredService<CombatController>();

    private async Task<IRenderedComponent<StartCombatDialog>> RenderAsync()
    {
        await OpenMapAsync(MapState.Empty with
        {
            Tokens = [GenericToken("a", "Goblin"), GenericToken("b", "Orco"), NoteToken("c", "Lyra", "Personajes/lyra.md")],
        });
        Shell.OpenStartCombat();
        return Render<StartCombatDialog>();
    }

    private static AngleSharp.Dom.IElement Start(IRenderedComponent<StartCombatDialog> cut) =>
        cut.FindAll("button").First(b => b.TextContent.Trim() == "Empezar");

    private static string[] Names(IRenderedComponent<StartCombatDialog> cut) =>
        [.. cut.FindAll("[data-testid=combat-token]").Select(r => r.GetAttribute("data-name")!)];

    private string[] OrderedNames() =>
        [.. Combat.State!.Entries.Select(e => Tokens.First(t => t.Id == e.TokenId).Name)];

    [Fact]
    public async Task Lists_every_map_token_checked()
    {
        var cut = await RenderAsync();

        cut.FindAll("input[type=checkbox]").Select(c => c.HasAttribute("checked")).Should().AllBeEquivalentTo(true).And.HaveCount(3);
    }

    [Fact]
    public async Task Lists_tokens_in_map_order()
    {
        var cut = await RenderAsync();

        Names(cut).Should().Equal("Goblin", "Orco", "Lyra");
    }

    [Fact]
    public async Task Unchecked_tokens_do_not_join()
    {
        var cut = await RenderAsync();
        cut.FindAll("input[type=checkbox]")[1].Change(false);

        Start(cut).Click();

        OrderedNames().Should().BeEquivalentTo("Goblin", "Lyra");
    }

    [Fact]
    public async Task Typing_a_number_and_pressing_enter_focuses_the_next_field()
    {
        var cut = await RenderAsync();
        Field(cut, "Goblin").Input("18");

        Field(cut, "Goblin").KeyDown(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Enter" });

        JSInterop.VerifyFocusAsyncInvoke();
    }

    [Fact]
    public async Task Enter_on_consecutive_fields_focuses_different_fields()
    {
        var cut = await RenderAsync();
        Field(cut, "Goblin").KeyDown(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Enter" });
        Field(cut, "Orco").KeyDown(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Enter" });

        JSInterop.Invocations.Where(i => i.Identifier.EndsWith("focus")).Select(i => ((Microsoft.AspNetCore.Components.ElementReference)i.Arguments[0]!).Id)
            .Distinct().Should().HaveCount(2);
    }

    [Fact]
    public async Task Enter_on_the_last_field_does_not_move_focus()
    {
        var cut = await RenderAsync();

        Field(cut, "Lyra").KeyDown(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Enter" });

        JSInterop.Invocations.Should().NotContain(i => i.Identifier.EndsWith("focus"));
    }

    [Fact]
    public async Task Enter_on_the_last_field_does_not_start()
    {
        var cut = await RenderAsync();

        Field(cut, "Lyra").KeyDown(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Enter" });

        Combat.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Start_orders_entries_by_the_typed_numbers()
    {
        var cut = await RenderAsync();
        Field(cut, "Goblin").Input("9");
        Field(cut, "Orco").Input("18");
        Field(cut, "Lyra").Input("15");

        Start(cut).Click();

        OrderedNames().Should().Equal("Orco", "Lyra", "Goblin");
    }

    [Fact]
    public async Task Tokens_without_number_join_last()
    {
        var cut = await RenderAsync();
        Field(cut, "Lyra").Input("3");

        Start(cut).Click();

        OrderedNames().Should().Equal("Lyra", "Goblin", "Orco");
    }

    [Fact]
    public async Task Non_numeric_text_shows_an_error()
    {
        var cut = await RenderAsync();
        Field(cut, "Goblin").Input("abc");

        Start(cut).Click();

        cut.Markup.Should().Contain("Escribe un número entero");
    }

    [Fact]
    public async Task Non_numeric_text_does_not_start()
    {
        var cut = await RenderAsync();
        Field(cut, "Goblin").Input("abc");

        Start(cut).Click();

        Combat.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Negative_number_is_a_valid_initiative()
    {
        var cut = await RenderAsync();
        Field(cut, "Goblin").Input("-1");
        Field(cut, "Lyra").Input("3");

        Start(cut).Click();

        OrderedNames().Should().Equal("Lyra", "Goblin", "Orco");
    }

    [Fact]
    public async Task Starting_closes_the_dialog()
    {
        var cut = await RenderAsync();

        Start(cut).Click();

        Shell.StartCombatOpen.Should().BeFalse();
    }

    [Fact]
    public async Task Fields_use_the_numeric_keyboard()
    {
        var cut = await RenderAsync();

        Field(cut, "Goblin").GetAttribute("inputmode").Should().Be("numeric");
    }
}
