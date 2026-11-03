using DmCompanion.UI.Components;
using DmCompanion.UI.Tests.Support;

namespace DmCompanion.UI.Tests;

public class SelectTests : UiTestContext
{
    private sealed record NoteKind(string Name);

    private static readonly NoteKind[] Kinds = [new("Nota"), new("PNJ"), new("Lugar")];

    private IRenderedComponent<Select<NoteKind>> RenderSelect(Action<ComponentParameterCollectionBuilder<Select<NoteKind>>>? configure = null, NoteKind? value = null) =>
        Render<Select<NoteKind>>(p =>
        {
            p.Add(x => x.Label, "Tipo")
             .Add(x => x.Options, Kinds)
             .Add(x => x.LabelOf, k => k.Name)
             .Add(x => x.Value, value ?? Kinds[0]);
            configure?.Invoke(p);
        });

    [Fact]
    public void Choosing_the_second_option_emits_that_option()
    {
        NoteKind? chosen = null;
        var cut = RenderSelect(p => p.Add(x => x.ValueChanged, (NoteKind k) => chosen = k));
        cut.Find("select").Change("1");
        chosen.Should().Be(Kinds[1]);
    }

    [Fact]
    public void Invalid_choice_is_ignored()
    {
        var calls = 0;
        var cut = RenderSelect(p => p.Add(x => x.ValueChanged, (NoteKind _) => calls++));
        cut.Find("select").Change("9");
        calls.Should().Be(0);
    }

    [Fact]
    public void Option_matching_Value_is_selected()
    {
        var cut = RenderSelect(value: Kinds[2]);
        cut.FindAll("option").Single(o => o.HasAttribute("selected")).TextContent.Should().Be("Lugar");
    }

    [Fact]
    public void Options_show_the_label_of_each_value()
    {
        var cut = RenderSelect();
        cut.FindAll("option").Select(o => o.TextContent).Should().Equal("Nota", "PNJ", "Lugar");
    }

    [Fact]
    public void Label_wraps_the_select()
    {
        var cut = RenderSelect();
        cut.FindAll("label select").Should().HaveCount(1);
    }

    [Fact]
    public void Disabled_sets_the_disabled_attribute()
    {
        var cut = RenderSelect(p => p.Add(x => x.Disabled, true));
        cut.Find("select").HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public void Placeholder_for_value_outside_options_is_hidden()
    {
        var cut = RenderSelect(value: new NoteKind("Otro"));
        var placeholder = cut.FindAll("option").First();
        placeholder.HasAttribute("hidden").Should().BeTrue();
    }

    [Fact]
    public void Placeholder_for_value_outside_options_is_selected()
    {
        var cut = RenderSelect(value: new NoteKind("Otro"));
        var placeholder = cut.FindAll("option").First();
        placeholder.HasAttribute("selected").Should().BeTrue();
    }

    [Fact]
    public void Placeholder_for_value_outside_options_is_disabled()
    {
        var cut = RenderSelect(value: new NoteKind("Otro"));
        var placeholder = cut.FindAll("option").First();
        placeholder.HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public void No_placeholder_when_Value_is_in_Options()
    {
        var cut = RenderSelect();
        cut.FindAll("option").Should().HaveCount(3);
    }

    [Fact]
    public void Extra_attributes_reach_the_select()
    {
        var cut = RenderSelect(p => p.AddUnmatched("data-test", "tipo"));
        cut.Find("select").GetAttribute("data-test").Should().Be("tipo");
    }

    [Fact]
    public void Extra_class_is_appended_to_the_select_classes()
    {
        var cut = RenderSelect(p => p.AddUnmatched("class", "mi-select"));
        cut.Find("select").ClassList.Should().Contain(["ds-field__input", "ds-field__select", "mi-select"]);
    }
}
