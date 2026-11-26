using Bunit;
using DmCompanion.Core.Combat;
using DmCompanion.Core.Maps;
using DmCompanion.Screens.Components;
using DmCompanion.Screens.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DmCompanion.Screens.Tests;

public sealed class CombatNoticesTests : CombatUiTestBase
{
    private string EntryOf(string tokenId) => Combat.State!.Entries.First(e => e.TokenId == tokenId).Id;

    private MapToken Goblin => Tokens.First(t => t.Id == "g");

    private async Task<IRenderedComponent<DmShell>> RenderWithExpiredAsync()
    {
        await OpenMapAsync(MapState.Empty with { Tokens = [GenericToken("a", "Ana", hp: 20), GenericToken("g", "Goblin 2", hp: 40)] });
        Combat.Start([new InitiativeEntry("a", 15), new InitiativeEntry("g", 10)]);
        Combat.AddCondition("g", new ConditionDraft("frightened", "Asustado", null, 1, EntryOf("g")));
        Combat.NextTurn();
        Combat.NextTurn();
        Combat.NextTurn();
        return Render<DmShell>();
    }

    [Fact]
    public async Task Expired_condition_notice_uses_token_and_condition_names()
    {
        var cut = await RenderWithExpiredAsync();

        cut.Find("[data-testid=notice-expired]").TextContent.Should().Contain("A Goblin 2 se le acaba Asustado");
    }

    [Fact]
    public async Task Expired_condition_notice_offers_remove_and_keep()
    {
        var cut = await RenderWithExpiredAsync();

        cut.FindAll("[data-testid=notice-expired] button").Select(b => b.TextContent.Trim()).Should().Equal("Quitar", "Mantener");
    }

    [Fact]
    public async Task Remove_deletes_the_condition_and_the_notice()
    {
        var cut = await RenderWithExpiredAsync();

        await cut.InvokeAsync(() => cut.FindAll("[data-testid=notice-expired] button").First(b => b.TextContent.Trim() == "Quitar").Click());

        Goblin.ConditionList.Should().BeEmpty();
    }

    [Fact]
    public async Task Remove_also_clears_the_notice()
    {
        var cut = await RenderWithExpiredAsync();

        await cut.InvokeAsync(() => cut.FindAll("[data-testid=notice-expired] button").First(b => b.TextContent.Trim() == "Quitar").Click());

        cut.FindAll("[data-testid=notice-expired]").Should().BeEmpty();
    }

    [Fact]
    public async Task Keep_without_duration_clears_the_expired_mark()
    {
        var cut = await RenderWithExpiredAsync();
        await cut.InvokeAsync(() => cut.FindAll("[data-testid=notice-expired] button").First(b => b.TextContent.Trim() == "Mantener").Click());

        await cut.InvokeAsync(() => cut.Find("[data-testid=keep-forever]").Click());

        Goblin.ConditionList.Single().Should().Match<TokenCondition>(c => !c.Expired && c.Duration == null);
    }

    [Fact]
    public async Task Keep_with_rounds_restarts_the_duration()
    {
        var cut = await RenderWithExpiredAsync();
        await cut.InvokeAsync(() => cut.FindAll("[data-testid=notice-expired] button").First(b => b.TextContent.Trim() == "Mantener").Click());

        await cut.InvokeAsync(() => cut.Find("[data-testid=keep-rounds]").Input("2"));
        await cut.InvokeAsync(() => cut.Find("[data-testid=keep-apply]").Click());

        Goblin.ConditionList.Single().Duration.Should().Be(new ConditionDuration(EntryOf("g"), 2));
    }

    [Fact]
    public async Task Dismissed_notice_keeps_the_condition_marked_expired()
    {
        await RenderWithExpiredAsync();

        Combat.Dismiss(Combat.Notices.Single());

        Goblin.ConditionList.Single().Expired.Should().BeTrue();
    }

    [Fact]
    public async Task Expired_notice_close_button_dismisses_it_and_keeps_the_condition()
    {
        var cut = await RenderWithExpiredAsync();

        await cut.InvokeAsync(() => cut.Find("[data-testid=notice-expired]").Closest(".ds-notice")!.QuerySelector("button[aria-label='Cerrar aviso']")!.Click());

        (cut.FindAll("[data-testid=notice-expired]").Count, Goblin.ConditionList.Single().Expired).Should().Be((0, true));
    }

    [Fact]
    public async Task Ended_notice_close_button_dismisses_it()
    {
        var cut = await RenderWithExpiredAsync();
        await cut.InvokeAsync(() => Combat.End());
        cut.WaitForAssertion(() => cut.FindAll("[data-testid=notice-ended]").Should().ContainSingle());

        await cut.InvokeAsync(() => cut.Find("[data-testid=notice-ended]").Closest(".ds-notice")!.QuerySelector("button[aria-label='Cerrar aviso']")!.Click());

        cut.FindAll("[data-testid=notice-ended]").Should().BeEmpty();
    }

    [Fact]
    public async Task Notices_with_their_own_dismiss_button_get_no_extra_close_button()
    {
        var cut = await RenderWithExpiredAsync();
        await cut.InvokeAsync(() => Combat.AddCondition("g", new ConditionDraft("concentration", "Concentración", null, null, null)));
        await cut.InvokeAsync(() => Combat.Damage("g", 22));

        cut.WaitForAssertion(() => cut.Find("[data-testid=notice-concentration]"));

        cut.Find("[data-testid=notice-concentration]").Closest(".ds-notice")!.QuerySelectorAll("button[aria-label='Cerrar aviso']").Should().BeEmpty();
    }

    [Fact]
    public async Task Concentration_notice_shows_the_dc()
    {
        var cut = await RenderWithExpiredAsync();
        await cut.InvokeAsync(() => Combat.AddCondition("g", new ConditionDraft("concentration", "Concentración", null, null, null)));

        await cut.InvokeAsync(() => Combat.Damage("g", 22));

        cut.WaitForAssertion(() => cut.Find("[data-testid=notice-concentration]").TextContent
            .Should().Contain("Goblin 2: Salvación de Constitución para mantener la concentración: CD 11"));
    }

    [Fact]
    public async Task Notice_for_a_removed_token_is_discarded_silently()
    {
        var cut = await RenderWithExpiredAsync();

        await cut.InvokeAsync(() => Session.Mutate(e => e.ReplaceState(s => s with { Tokens = s.Tokens.Where(t => t.Id != "g").ToList() })));

        cut.WaitForAssertion(() => Combat.Notices.Should().BeEmpty());
    }

    [Fact]
    public async Task Notice_for_a_removed_condition_is_discarded_silently()
    {
        var cut = await RenderWithExpiredAsync();

        Session.Mutate(e => e.ReplaceState(s => s with
        {
            Tokens = s.Tokens.Select(t => t with { Conditions = null }).ToList(),
        }));

        cut.WaitForAssertion(() => cut.FindAll("[data-testid=notice-expired]").Should().BeEmpty());
    }

    [Fact]
    public async Task Collapsed_strip_notices_go_to_the_map_notices_stack()
    {
        var cut = await RenderWithExpiredAsync();
        await cut.InvokeAsync(() => cut.Find("[data-testid=strip-toggle]").Click());

        cut.WaitForAssertion(() => cut.FindAll(".map-layout .map-notices [data-testid=combat-notices]").Should().HaveCount(1));
    }

    [Fact]
    public async Task Expanded_strip_notices_do_not_go_to_the_map_notices_stack()
    {
        var cut = await RenderWithExpiredAsync();

        cut.FindAll(".map-notices [data-testid=combat-notices]").Should().BeEmpty();
    }

    [Fact]
    public async Task Read_only_disables_remove_and_keep()
    {
        await RenderWithExpiredAsync();
        MakeReadOnly();
        var cut = Render<DmShell>();

        cut.FindAll("[data-testid=notice-expired] button").Select(b => b.HasAttribute("disabled")).Should().Equal(true, true);
    }

    [Fact]
    public async Task Read_only_never_dismisses_the_expired_notice()
    {
        await RenderWithExpiredAsync();
        MakeReadOnly();
        var cut = Render<DmShell>();

        await cut.InvokeAsync(() => cut.FindAll("[data-testid=notice-expired] button")[0].Click());

        Combat.Notices.Should().ContainSingle();
    }

    [Fact]
    public async Task Read_only_expired_notice_has_no_close_button()
    {
        await RenderWithExpiredAsync();
        MakeReadOnly();
        var cut = Render<DmShell>();

        cut.Find("[data-testid=notice-expired]").Closest(".ds-notice")!.QuerySelectorAll("button[aria-label='Cerrar aviso']").Should().BeEmpty();
    }
}
