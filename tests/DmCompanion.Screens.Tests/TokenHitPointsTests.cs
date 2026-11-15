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
}
