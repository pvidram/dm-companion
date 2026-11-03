using DmCompanion.UI.Components;
using DmCompanion.UI.Tests.Support;

namespace DmCompanion.UI.Tests;

public class TextAreaTests : UiTestContext
{
    private IRenderedComponent<TextArea> RenderArea(Action<ComponentParameterCollectionBuilder<TextArea>>? configure = null) =>
        Render<TextArea>(p =>
        {
            p.Add(x => x.Label, "Notas");
            configure?.Invoke(p);
        });

    [Fact]
    public void Typing_emits_the_text()
    {
        string? value = null;
        var cut = RenderArea(p => p.Add(x => x.ValueChanged, (string v) => value = v));
        cut.Find("textarea").Input("hola\nmundo");
        value.Should().Be("hola\nmundo");
    }

    [Fact]
    public void Rows_attribute_reflects_Rows()
    {
        var cut = RenderArea(p => p.Add(x => x.Rows, 4));
        cut.Find("textarea").GetAttribute("rows").Should().Be("4");
    }

    [Fact]
    public void Rows_default_to_eight()
    {
        var cut = RenderArea();
        cut.Find("textarea").GetAttribute("rows").Should().Be("8");
    }

    [Fact]
    public void Label_wraps_the_textarea()
    {
        var cut = RenderArea();
        cut.FindAll("label textarea").Should().HaveCount(1);
    }

    [Fact]
    public void Disabled_sets_the_disabled_attribute()
    {
        var cut = RenderArea(p => p.Add(x => x.Disabled, true));
        cut.Find("textarea").HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public void Extra_attributes_reach_the_textarea()
    {
        var cut = RenderArea(p => p.AddUnmatched("data-test", "notas"));
        cut.Find("textarea").GetAttribute("data-test").Should().Be("notas");
    }

    [Fact]
    public void Extra_class_is_appended_to_the_textarea_classes()
    {
        var cut = RenderArea(p => p.AddUnmatched("class", "mi-area"));
        cut.Find("textarea").ClassList.Should().Contain(["ds-field__input", "ds-field__input--multiline", "mi-area"]);
    }
}
