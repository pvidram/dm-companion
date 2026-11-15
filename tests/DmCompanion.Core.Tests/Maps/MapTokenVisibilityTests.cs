using DmCompanion.Core.Maps;

namespace DmCompanion.Core.Tests.Maps;

public class MapTokenVisibilityTests
{
    private static MapEditor NewEditor()
    {
        var n = 0;
        return new MapEditor(MapState.Empty, 1000, 500, () => $"id{++n}");
    }

    private static TokenDraft Generic() => new("Goblin", null, TokenSide.Enemy, 1, 15, 7);

    [Fact]
    public void SetHidden_marks_and_unmarks_the_token()
    {
        var ed = NewEditor();
        var t = ed.AddToken(Generic(), 10, 10);

        ed.SetHidden(t.Id, true).Should().BeTrue();
        ed.State.Tokens[0].Hidden.Should().BeTrue();

        ed.Undo().Should().BeTrue();
        ed.State.Tokens[0].Hidden.Should().BeNull();
    }

    [Fact]
    public void SetHidden_redo_hides_the_token_again()
    {
        var ed = NewEditor();
        var t = ed.AddToken(Generic(), 10, 10);
        ed.SetHidden(t.Id, true);
        ed.Undo();

        ed.Redo().Should().BeTrue();

        ed.State.Tokens[0].Hidden.Should().BeTrue();
    }

    [Fact]
    public void SetHidden_false_stores_null()
    {
        var ed = NewEditor();
        var t = ed.AddToken(Generic(), 10, 10);
        ed.SetHidden(t.Id, true);

        ed.SetHidden(t.Id, false).Should().BeTrue();

        ed.State.Tokens[0].Hidden.Should().BeNull();
    }

    [Fact]
    public void SetHidden_returns_false_when_nothing_changes()
    {
        var ed = NewEditor();
        var t = ed.AddToken(Generic(), 10, 10);

        ed.SetHidden(t.Id, false).Should().BeFalse();
    }

    [Fact]
    public void SetHidden_returns_false_for_unknown_token()
    {
        NewEditor().SetHidden("nope", true).Should().BeFalse();
    }

    [Fact]
    public void UpdateToken_sets_and_clears_alias()
    {
        var ed = NewEditor();
        var t = ed.AddToken(Generic(), 10, 10);

        ed.UpdateToken(t.Id, new TokenEdit(TokenSide.Enemy, 1, "Goblin", 15, 7, "Criatura"));
        ed.State.Tokens[0].Alias.Should().Be("Criatura");

        ed.UpdateToken(t.Id, new TokenEdit(TokenSide.Enemy, 1, "Goblin", 15, 7, "  "));
        ed.State.Tokens[0].Alias.Should().BeNull();
    }

    [Fact]
    public void UpdateToken_sets_alias_on_tokens_with_source()
    {
        var ed = NewEditor();
        var t = ed.AddToken(new TokenDraft("Goblin", "Personajes/Goblin.md", TokenSide.Enemy, 1, null, null), 10, 10);

        ed.UpdateToken(t.Id, new TokenEdit(TokenSide.Enemy, 1, null, null, null, " Sombra "));

        ed.State.Tokens[0].Alias.Should().Be("Sombra");
    }

    [Fact]
    public void PlayerName_uses_alias_when_present()
    {
        var token = new MapToken("t", "Goblin", null, TokenSide.Enemy, 1, 0, 0, null, null);

        token.PlayerName.Should().Be("Goblin");
        (token with { Alias = " Sombra " }).PlayerName.Should().Be("Sombra");
        (token with { Alias = " " }).PlayerName.Should().Be("Goblin");
    }

    [Fact]
    public void IsHidden_is_true_only_when_hidden()
    {
        var token = new MapToken("t", "Goblin", null, TokenSide.Enemy, 1, 0, 0, null, null);

        token.IsHidden.Should().BeFalse();
        (token with { Hidden = true }).IsHidden.Should().BeTrue();
    }
}
