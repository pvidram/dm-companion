using Bunit;
using DmCompanion.Core.Combat;
using DmCompanion.Core.Maps;
using DmCompanion.Screens.Components;
using DmCompanion.Screens.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DmCompanion.Screens.Tests;

/// <summary>Estados y HP temporales editables desde la tarjeta del token, también sin combate.</summary>
public sealed class TokenCardConditionsTests : TokenTestBase
{
    private static readonly TokenCondition Frightened = new("c1", "frightened", "Asustado", null, null, false);

    private async Task<IRenderedComponent<TokenCard>> CardAsync(MapToken token, bool readOnly = false)
    {
        await OpenMapAsync(MapState.Empty with { Tokens = [token] });
        if (readOnly) typeof(MapSession).GetProperty(nameof(MapSession.IsReadOnly))!.SetValue(Session, true);
        Shell.OpenTokenCard(new TokenCardRequest(token.Id, 100, 100));
        return Render<TokenCard>();
    }

    private static MapToken Goblin() => GenericToken("g", "Goblin", hp: 7);

    [Fact]
    public async Task Card_removes_a_condition_without_combat()
    {
        var cut = await CardAsync(Goblin() with { Conditions = [Frightened] });

        cut.Find("[data-testid=token-conditions] button[aria-label='Quitar Asustado']").Click();

        Tokens[0].ConditionList.Should().BeEmpty();
    }

    [Fact]
    public async Task Card_adds_a_condition_without_combat()
    {
        var cut = await CardAsync(Goblin());

        cut.Find("[data-testid=token-add-condition]").Click();
        cut.Find("[data-testid=condition-save]").Click();

        Tokens[0].ConditionList.Should().ContainSingle();
    }

    [Fact]
    public async Task Card_condition_editor_has_no_rounds_without_combat()
    {
        var cut = await CardAsync(Goblin());

        cut.Find("[data-testid=token-add-condition]").Click();

        cut.FindAll("[data-testid=condition-rounds]").Should().BeEmpty();
    }

    [Fact]
    public async Task Card_clears_temp_hp()
    {
        var cut = await CardAsync(Goblin() with { HpTemp = 5 });

        cut.Find("[data-testid=token-temp-clear]").Click();

        Tokens[0].TempHp.Should().Be(0);
    }

    [Fact]
    public async Task Card_hides_condition_editing_when_read_only()
    {
        var cut = await CardAsync(Goblin() with { Conditions = [Frightened], HpTemp = 5 }, readOnly: true);

        cut.FindAll("[data-testid=token-add-condition], [data-testid=token-temp-clear], [data-testid=token-conditions] button").Should().BeEmpty();
    }
}
