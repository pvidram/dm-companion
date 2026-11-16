using DmCompanion.Core.Maps;
using DmCompanion.Screens.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DmCompanion.Screens.Tests;

public sealed class TokenHitPointsTests : TokenTestBase
{
    private TokenHitPoints Hp => Services.GetRequiredService<TokenHitPoints>();

    [Fact]
    public async Task Max_of_note_token_comes_from_the_note()
    {
        var token = NoteToken("t", "Lyra", MapTestVault.LyraNote);
        await OpenMapAsync(MapState.Empty with { Tokens = [token] },
            extraNotes: () => WriteNote(MapTestVault.LyraNote, "---\ntype: pc\nname: Lyra\nstats:\n  hp: 11\n---\n"));

        Hp.MaxFor(token).Should().Be(11);
    }

    [Fact]
    public async Task Max_of_generic_token_is_its_hp()
    {
        await OpenMapAsync();

        Hp.MaxFor(GenericToken("g", "Goblin", hp: 7)).Should().Be(7);
    }

    [Fact]
    public async Task Max_of_token_whose_note_was_deleted_is_null()
    {
        await OpenMapAsync();

        Hp.MaxFor(NoteToken("t", "Fantasma", "Personajes/borrado.md")).Should().BeNull();
    }

    [Fact]
    public async Task Srd_token_max_hp_comes_from_the_srd()
    {
        var token = NoteToken("t", "Aboleto", "srd:criaturas/aboleth", TokenSide.Enemy);
        await OpenMapAsync(MapState.Empty with { Tokens = [token] });

        Hp.MaxFor(token).Should().Be(150);
    }

    [Fact]
    public async Task Two_srd_tokens_track_hp_separately()
    {
        var first = NoteToken("a", "Guerrero goblin", "srd:criaturas/goblin-warrior", TokenSide.Enemy) with { HpCurrent = 3 };
        var second = NoteToken("b", "Guerrero goblin 2", "srd:criaturas/goblin-warrior", TokenSide.Enemy) with { HpCurrent = 6 };
        await OpenMapAsync(MapState.Empty with { Tokens = [first, second] });

        (Tokens[0].HpCurrent, Tokens[1].HpCurrent, Hp.MaxFor(Tokens[0]), Hp.MaxFor(Tokens[1])).Should().Be((3, 6, 10, 10));
    }
}
