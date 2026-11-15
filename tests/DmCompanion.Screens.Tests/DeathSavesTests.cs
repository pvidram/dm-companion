using AngleSharp.Dom;
using Bunit;
using DmCompanion.Core.Combat;
using DmCompanion.Core.Maps;
using DmCompanion.Screens.Components;
using DmCompanion.Screens.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DmCompanion.Screens.Tests;

public sealed class DeathSavesTests : CombatUiTestBase
{

    [Fact]
    public async Task Pc_at_zero_hp_shows_death_saves_and_marks_them()
    {
        var cut = await RenderAsync(GenericToken("a", "Ana", hp: 20, side: TokenSide.Pc) with { HpCurrent = 0 });

        cut.Find("[data-testid=save-failure-2]").Change(true);

        Ana.DeathSaves.Should().Be(new DeathSaves(0, 2));
    }

    [Fact]
    public async Task Unchecking_a_save_lowers_the_count()
    {
        var cut = await RenderAsync(GenericToken("a", "Ana", hp: 20, side: TokenSide.Pc) with { HpCurrent = 0, DeathSaves = new DeathSaves(2, 0) });

        cut.Find("[data-testid=save-success-2]").Change(false);

        Ana.DeathSaves.Should().Be(new DeathSaves(1, 0));
    }

    [Fact]
    public async Task Enemy_at_zero_hp_does_not_show_death_saves()
    {
        var cut = await RenderAsync(GenericToken("a", "Ana", hp: 20) with { HpCurrent = 0 });

        cut.FindAll("[data-testid=save-failure-1]").Should().BeEmpty();
    }

    [Fact]
    public async Task Pc_with_hp_does_not_show_death_saves()
    {
        var cut = await RenderAsync(GenericToken("a", "Ana", hp: 20, side: TokenSide.Pc));

        cut.FindAll("[data-testid=save-failure-1]").Should().BeEmpty();
    }
}
