using System.Globalization;
using DmCompanion.UI.Components;
using DmCompanion.UI.Tests.Support;

namespace DmCompanion.UI.Tests;

public class NumberStepperTests : UiTestContext
{
    [Fact]
    public void Value_is_formatted_with_invariant_culture_and_decimals()
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("es-ES");
        try
        {
            var cut = Render<NumberStepper>(p => p.Add(x => x.Label, "Celda").Add(x => x.Value, 70.5).Add(x => x.Decimals, 1));
            cut.Find("input").GetAttribute("value").Should().Be("70.5");
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void Plus_adds_the_step_without_floating_point_drift()
    {
        double? received = null;
        var cut = Render<NumberStepper>(p => p.Add(x => x.Label, "Celda").Add(x => x.Value, 70.2).Add(x => x.Step, 0.1)
            .Add(x => x.Decimals, 1).Add(x => x.ValueChanged, (double v) => received = v));
        cut.Find("button[aria-label='Aumentar Celda']").Click();
        received.Should().Be(70.3);
    }

    [Fact]
    public void Minus_subtracts_the_step()
    {
        double? received = null;
        var cut = Render<NumberStepper>(p => p.Add(x => x.Label, "Desplazamiento X").Add(x => x.Value, 3)
            .Add(x => x.ValueChanged, (double v) => received = v));
        cut.Find("button[aria-label='Disminuir Desplazamiento X']").Click();
        received.Should().Be(2);
    }

    [Fact]
    public void Typed_value_accepts_a_decimal_comma_and_rounds_to_decimals()
    {
        double? received = null;
        var cut = Render<NumberStepper>(p => p.Add(x => x.Label, "Celda").Add(x => x.Value, 70).Add(x => x.Decimals, 1)
            .Add(x => x.ValueChanged, (double v) => received = v));
        cut.Find("input").Change("70,35");
        received.Should().Be(70.4);
    }

    [Fact]
    public void Typed_value_is_clamped_to_max()
    {
        double? received = null;
        var cut = Render<NumberStepper>(p => p.Add(x => x.Label, "Celda").Add(x => x.Value, 70).Add(x => x.Max, 100)
            .Add(x => x.ValueChanged, (double v) => received = v));
        cut.Find("input").Change("250");
        received.Should().Be(100);
    }

    [Fact]
    public void Invalid_typed_value_emits_nothing()
    {
        var emitted = false;
        var cut = Render<NumberStepper>(p => p.Add(x => x.Label, "Celda").Add(x => x.Value, 70)
            .Add(x => x.ValueChanged, (double _) => emitted = true));
        cut.Find("input").Change("abc");
        emitted.Should().BeFalse();
    }

    [Fact]
    public void Plus_is_disabled_at_max()
    {
        var cut = Render<NumberStepper>(p => p.Add(x => x.Label, "Celda").Add(x => x.Value, 100).Add(x => x.Max, 100));
        cut.Find("button[aria-label='Aumentar Celda']").HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public void Minus_is_disabled_at_min()
    {
        var cut = Render<NumberStepper>(p => p.Add(x => x.Label, "Celda").Add(x => x.Value, 1).Add(x => x.Min, 1));
        cut.Find("button[aria-label='Disminuir Celda']").HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public void Input_accessible_name_includes_the_unit()
    {
        var cut = Render<NumberStepper>(p => p.Add(x => x.Label, "Celda").Add(x => x.Unit, "px"));
        cut.Find("input").GetAttribute("aria-label").Should().Be("Celda (px)");
    }

    [Fact]
    public void Stepper_is_a_group_labelled_by_its_visible_label()
    {
        var cut = Render<NumberStepper>(p => p.Add(x => x.Label, "Celda"));
        var labelId = cut.Find(".ds-stepper > .ds-field__label").Id;
        cut.Find(".ds-stepper[role='group']").GetAttribute("aria-labelledby").Should().Be(labelId);
    }

    [Fact]
    public void Typing_minus_zero_emits_positive_zero()
    {
        double? received = null;
        var cut = Render<NumberStepper>(p => p.Add(x => x.Label, "Celda").Add(x => x.Value, 5).Add(x => x.ValueChanged, (double v) => received = v));
        cut.Find("input").Change("-0");
        double.IsNegative(received!.Value).Should().BeFalse();
    }

    [Fact]
    public void Small_negative_value_without_decimals_shows_zero()
    {
        var cut = Render<NumberStepper>(p => p.Add(x => x.Label, "Celda").Add(x => x.Value, -0.3).Add(x => x.Decimals, 0));
        cut.Find("input").GetAttribute("value").Should().Be("0");
    }
}
