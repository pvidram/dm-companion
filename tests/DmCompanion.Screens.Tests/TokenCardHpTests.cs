using System.Text.RegularExpressions;
using Bunit;
using DmCompanion.Core.Maps;
using DmCompanion.Screens.Components;
using DmCompanion.Screens.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace DmCompanion.Screens.Tests;

/// <summary>HP actuales editables en la tarjeta del token (spec §7.9).</summary>
public sealed partial class TokenCardHpTests : TokenTestBase
{
    private const string CosmereCampaign = "---\ntype: campaign\nname: Campaña\nsystem: cosmere\n---\n";

    private static string Lyra(string stats) => $"---\ntype: pc\nname: Lyra\nstats:\n{stats}---\n";

    private static readonly string Lyra11 = Lyra("  hp: 11\n");

    private async Task<IRenderedComponent<TokenCard>> CardAsync(MapToken token, string? lyra = null, string? campaign = null)
    {
        await OpenMapAsync(MapState.Empty with { Tokens = [token] }, extraNotes: () =>
        {
            WriteNote(MapTestVault.LyraNote, lyra ?? Lyra11);
            if (campaign is not null) WriteNote("Campana.md", campaign);
        });
        Shell.OpenTokenCard(new TokenCardRequest(token.Id, 100, 100));
        return Render<TokenCard>();
    }

    private static MapToken LyraToken(int? hpCurrent = null) => NoteToken("t", "Lyra", MapTestVault.LyraNote) with { HpCurrent = hpCurrent };

    private static string Row(IRenderedComponent<TokenCard> cut) =>
        Whitespace().Replace(cut.Find("[data-testid=token-hp]").TextContent, " ").Trim();

    private static AngleSharp.Dom.IElement? ValueButton(IRenderedComponent<TokenCard> cut) =>
        cut.FindAll("button[data-testid=token-hp-value]").FirstOrDefault();

    private static void Enter(IRenderedComponent<TokenCard> cut, string text)
    {
        ValueButton(cut)!.Click();
        var input = cut.Find("[data-testid=token-hp-input]");
        input.Input(text);
        input.KeyDown(new KeyboardEventArgs { Key = "Enter" });
    }

    // Un navegador puede disparar blur sobre el input al retirarlo del DOM. bUnit no deja usar el elemento caducado,
    // así que se lanza el evento con el id de su manejador, tal como llegaría desde el navegador.
    private async Task BlurStaleAsync(ulong blurHandlerId)
    {
        try
        {
            await DispatchBlurAsync(blurHandlerId);
        }
        catch (ArgumentException)
        {
            // Blazor descarta los eventos de manejadores ya retirados: es el resultado correcto.
        }
    }

    private Task DispatchBlurAsync(ulong id) =>
        Renderer.Dispatcher.InvokeAsync(() => Renderer.DispatchEventAsync(id, null!, new FocusEventArgs()));

    private static ulong BlurHandlerId(AngleSharp.Dom.IElement input) => ulong.Parse(input.GetAttribute("blazor:onblur")!);

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    [Fact]
    public async Task Note_token_without_hp_current_shows_the_note_maximum()
    {
        var cut = await CardAsync(LyraToken());

        Row(cut).Should().Be("HP 11 / 11");
    }

    [Fact]
    public async Task Damage_entry_lowers_the_shown_value()
    {
        var cut = await CardAsync(LyraToken());

        Enter(cut, "-5");

        Row(cut).Should().Be("HP 6 / 11");
    }

    [Fact]
    public async Task Relative_entry_applies_to_the_current_value_not_to_the_one_rendered()
    {
        var cut = await CardAsync(LyraToken());
        cut.Find("button[data-testid=token-hp-value]").Click();
        var input = cut.Find("[data-testid=token-hp-input]");
        input.Input("-3");

        Session.Editor!.SetHp("t", 8, 11); // recarga externa que aún no se ha pintado
        input.KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Session.Editor!.State.Tokens.Single().HpCurrent.Should().Be(5);
    }

    [Fact]
    public async Task Damage_entry_is_stored_in_the_map_state()
    {
        var cut = await CardAsync(LyraToken());

        Enter(cut, "-5");

        Tokens[0].HpCurrent.Should().Be(6);
    }

    [Fact]
    public async Task Absolute_entry_sets_the_value()
    {
        var cut = await CardAsync(LyraToken(hpCurrent: 3));

        Enter(cut, "9");

        Tokens[0].HpCurrent.Should().Be(9);
    }

    [Fact]
    public async Task Healing_entry_is_clamped_to_the_maximum()
    {
        var cut = await CardAsync(LyraToken(hpCurrent: 9));

        Enter(cut, " +5 ");

        Tokens[0].HpCurrent.Should().Be(11);
    }

    [Fact]
    public async Task Damage_beyond_zero_is_clamped_to_zero()
    {
        var cut = await CardAsync(LyraToken(hpCurrent: 4));

        Enter(cut, "-50");

        Tokens[0].HpCurrent.Should().Be(0);
    }

    [Fact]
    public async Task Entry_equal_to_the_maximum_is_still_stored()
    {
        var cut = await CardAsync(LyraToken());

        Enter(cut, "11");

        Tokens[0].HpCurrent.Should().Be(11);
    }

    [Fact]
    public async Task Pressing_the_number_shows_an_input_with_a_decimal_keypad()
    {
        var cut = await CardAsync(LyraToken());

        ValueButton(cut)!.Click();

        cut.Find("[data-testid=token-hp-input]").GetAttribute("inputmode").Should().Be("decimal");
    }

    [Fact]
    public async Task Losing_focus_applies_the_entry()
    {
        var cut = await CardAsync(LyraToken());
        ValueButton(cut)!.Click();
        var input = cut.Find("[data-testid=token-hp-input]");
        input.Input("-2");

        input.Blur();

        Tokens[0].HpCurrent.Should().Be(9);
    }

    [Fact]
    public async Task Escape_leaves_the_state_untouched()
    {
        var cut = await CardAsync(LyraToken());
        ValueButton(cut)!.Click();
        var input = cut.Find("[data-testid=token-hp-input]");
        input.Input("-5");

        input.KeyDown(new KeyboardEventArgs { Key = "Escape" });

        Tokens[0].HpCurrent.Should().BeNull();
    }

    [Fact]
    public async Task Escape_closes_the_input()
    {
        var cut = await CardAsync(LyraToken());
        ValueButton(cut)!.Click();
        var input = cut.Find("[data-testid=token-hp-input]");
        input.Input("-5");

        input.KeyDown(new KeyboardEventArgs { Key = "Escape" });

        cut.FindAll("[data-testid=token-hp-input]").Should().BeEmpty();
    }

    [Fact]
    public async Task Invalid_entry_changes_nothing()
    {
        var cut = await CardAsync(LyraToken());

        Enter(cut, "abc");

        Tokens[0].HpCurrent.Should().BeNull();
    }

    [Fact]
    public async Task Invalid_entry_shows_a_notice()
    {
        var cut = await CardAsync(LyraToken());

        Enter(cut, "abc");

        cut.Find("[data-testid=token-hp-notice]").TextContent.Should().Contain("número");
    }

    [Fact]
    public async Task Valid_entry_after_an_invalid_one_clears_the_notice()
    {
        var cut = await CardAsync(LyraToken());
        Enter(cut, "abc");

        Enter(cut, "-1");

        cut.FindAll("[data-testid=token-hp-notice]").Should().BeEmpty();
    }

    [Fact]
    public async Task Empty_entry_is_a_silent_cancel()
    {
        var cut = await CardAsync(LyraToken());

        Enter(cut, "  ");

        cut.FindAll("[data-testid=token-hp-notice]").Should().BeEmpty();
    }

    [Fact]
    public async Task Read_only_map_shows_the_number_without_a_button()
    {
        await OpenMapAsync(extraNotes: () => WriteNote(MapTestVault.LyraNote, Lyra11));
        await Session.CloseAsync();
        File.WriteAllText(PathOf(MapPaths.StatePathFor(MapTestVault.MapNote)),
            "{\"version\":3,\"tokens\":[{\"id\":\"t\",\"name\":\"Orco\",\"side\":\"enemy\",\"size\":1,\"x\":35,\"y\":35,\"hp\":11}],\"strokes\":[]}");
        await Session.OpenAsync(MapTestVault.MapNote);
        Shell.OpenTokenCard(new TokenCardRequest("t", 100, 100));

        var cut = Render<TokenCard>();

        (Session.IsReadOnly, Row(cut), ValueButton(cut)).Should().Be((true, "HP 11 / 11", null));
    }

    [Fact]
    public async Task Generic_token_uses_its_own_hp_as_maximum()
    {
        var cut = await CardAsync(GenericToken("t", "Orco", hp: 15));

        Row(cut).Should().Be("HP 15 / 15");
    }

    [Fact]
    public async Task Generic_token_edit_is_stored_on_the_token()
    {
        var cut = await CardAsync(GenericToken("t", "Orco", hp: 15));

        Enter(cut, "-4");

        Tokens[0].HpCurrent.Should().Be(11);
    }

    [Fact]
    public async Task Generic_token_without_hp_shows_a_dash_and_no_button()
    {
        var cut = await CardAsync(GenericToken("t", "Orco", ac: 12));

        (Row(cut), ValueButton(cut)).Should().Be(("HP —", null));
    }

    [Fact]
    public async Task Note_without_a_hp_stat_shows_a_dash_and_no_button()
    {
        var cut = await CardAsync(LyraToken(), lyra: Lyra("  ac: 14\n"));

        (Row(cut), ValueButton(cut)).Should().Be(("HP —", null));
    }

    [Fact]
    public async Task Maximum_below_the_stored_value_shows_the_maximum()
    {
        var cut = await CardAsync(LyraToken(hpCurrent: 20));

        Row(cut).Should().Be("HP 11 / 11");
    }

    [Fact]
    public async Task Maximum_below_the_stored_value_does_not_rewrite_the_stored_value()
    {
        await CardAsync(LyraToken(hpCurrent: 20));

        Tokens[0].HpCurrent.Should().Be(20);
    }

    [Fact]
    public async Task Cosmere_note_token_labels_the_row_Salud_with_the_health_maximum()
    {
        var cut = await CardAsync(LyraToken(), Lyra("  health: { current: 3, max: 12 }\n"), CosmereCampaign);

        Row(cut).Should().Be("Salud 12 / 12");
    }

    [Fact]
    public async Task Dnd_card_does_not_show_the_hp_stat_twice()
    {
        var cut = await CardAsync(LyraToken(), Lyra("  ac: 14\n  hp: 11\n  speed: 30\n"));

        cut.FindAll(".ds-stat__label").Select(l => l.TextContent).Should().NotContain("HP");
    }

    [Fact]
    public async Task Editing_never_writes_the_note()
    {
        var cut = await CardAsync(LyraToken());
        var before = File.ReadAllBytes(PathOf(MapTestVault.LyraNote));

        Enter(cut, "-5");

        File.ReadAllBytes(PathOf(MapTestVault.LyraNote)).Should().Equal(before);
    }

    [Fact]
    public async Task Two_tokens_of_the_same_note_keep_separate_hp()
    {
        await OpenMapAsync(MapState.Empty with { Tokens = [LyraToken(), NoteToken("u", "Lyra 2", MapTestVault.LyraNote)] },
            extraNotes: () => WriteNote(MapTestVault.LyraNote, Lyra11));
        Shell.OpenTokenCard(new TokenCardRequest("t", 100, 100));
        var cut = Render<TokenCard>();

        Enter(cut, "-5");

        Tokens.Select(t => t.HpCurrent).Should().Equal(6, null);
    }

    [Fact]
    public async Task Escape_keeps_the_card_open_and_shows_the_number_button_again()
    {
        var cut = await CardAsync(LyraToken());
        ValueButton(cut)!.Click();
        var input = cut.Find("[data-testid=token-hp-input]");
        input.Input("-5");

        input.KeyDown(new KeyboardEventArgs { Key = "Escape" });

        (Shell.TokenCard, ValueButton(cut)).Should().Match<(TokenCardRequest?, AngleSharp.Dom.IElement?)>(t => t.Item1 != null && t.Item2 != null);
    }

    [Fact]
    public async Task Enter_followed_by_blur_applies_the_entry_only_once()
    {
        var cut = await CardAsync(LyraToken());
        ValueButton(cut)!.Click();
        var input = cut.Find("[data-testid=token-hp-input]");
        var blurId = BlurHandlerId(input);
        input.Input("-5");
        input.KeyDown(new KeyboardEventArgs { Key = "Enter" });

        await BlurStaleAsync(blurId);

        Tokens[0].HpCurrent.Should().Be(6);
    }

    [Fact]
    public async Task Escape_followed_by_blur_does_not_apply_the_typed_text()
    {
        var cut = await CardAsync(LyraToken());
        ValueButton(cut)!.Click();
        var input = cut.Find("[data-testid=token-hp-input]");
        var blurId = BlurHandlerId(input);
        input.Input("-5");
        input.KeyDown(new KeyboardEventArgs { Key = "Escape" });

        await BlurStaleAsync(blurId);

        Tokens[0].HpCurrent.Should().BeNull();
    }
}
