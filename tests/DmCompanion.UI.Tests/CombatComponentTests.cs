using DmCompanion.UI.Components;
using DmCompanion.UI.Tests.Support;

namespace DmCompanion.UI.Tests;

public class CombatComponentTests : UiTestContext
{
    [Fact]
    public void Stat_badge_shows_label_and_value()
    {
        var cut = Render<StatBadge>(p => p.Add(x => x.Label, "AC").Add(x => x.Value, "15"));
        cut.Find(".ds-stat").TextContent.Should().Contain("AC").And.Contain("15");
    }

    [Fact]
    public void Hp_control_shows_current_over_max()
    {
        var cut = Render<HpControl>(p => p.Add(x => x.Current, 12).Add(x => x.Max, 20));
        cut.Find(".ds-hp__value").TextContent.Should().Be("12 / 20");
    }

    [Fact]
    public void Damage_emits_the_typed_amount()
    {
        var received = 0;
        var cut = Render<HpControl>(p => p.Add(x => x.Current, 12).Add(x => x.Max, 20).Add(x => x.OnDamage, (int amount) => received = amount));
        cut.Find("input").Change("5");
        cut.Find(".ds-button--damage").Click();
        received.Should().Be(5);
    }

    [Fact]
    public void Heal_emits_one_by_default()
    {
        var received = 0;
        var cut = Render<HpControl>(p => p.Add(x => x.Current, 12).Add(x => x.Max, 20).Add(x => x.OnHeal, (int amount) => received = amount));
        cut.Find(".ds-button--heal").Click();
        received.Should().Be(1);
    }

    [Fact]
    public void Amount_below_one_disables_damage()
    {
        var cut = Render<HpControl>(p => p.Add(x => x.Current, 12).Add(x => x.Max, 20));
        cut.Find("input").Change("0");
        cut.Find(".ds-button--damage").HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public void Condition_tag_remove_button_names_the_condition()
    {
        var cut = Render<ConditionTag>(p => p.Add(x => x.Name, "Envenenado").Add(x => x.OnRemove, () => { }));
        cut.Find("button").GetAttribute("aria-label").Should().Be("Quitar Envenenado");
    }

    [Fact]
    public void ConditionTag_without_OnClick_has_no_button()
    {
        var cut = Render<ConditionTag>(p => p.Add(x => x.Name, "Derribado"));
        cut.FindAll("button").Should().BeEmpty();
    }

    [Fact]
    public void ConditionTag_with_OnClick_renders_a_labelled_button()
    {
        var clicks = 0;
        var cut = Render<ConditionTag>(p => p.Add(x => x.Name, "Derribado").Add(x => x.OnClick, () => clicks++));

        var button = cut.Find("button.ds-condition__name");
        button.Click();

        (button.GetAttribute("type"), button.GetAttribute("aria-label"), clicks).Should().Be(("button", "Ver regla: Derribado", 1));
    }
}
