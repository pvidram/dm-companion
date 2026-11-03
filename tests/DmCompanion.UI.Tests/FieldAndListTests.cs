using DmCompanion.UI.Components;
using DmCompanion.UI.Tests.Support;

namespace DmCompanion.UI.Tests;

public class FieldAndListTests : UiTestContext
{
    private sealed record Node(string Name, IReadOnlyList<Node> Children, bool Folder);

    private static readonly Node[] Tree =
    [
        new("Lugares", [new("Taberna", [], false)], true),
        new("Diario", [], false),
    ];

    private IRenderedComponent<TreeView<Node>> RenderNodeTree(Action<Node>? onSelect = null, string? selectedKey = null) =>
        Render<TreeView<Node>>(p => p
            .Add(x => x.Items, Tree)
            .Add(x => x.ChildrenOf, n => n.Children)
            .Add(x => x.LabelOf, n => n.Name)
            .Add(x => x.KeyOf, n => n.Name)
            .Add(x => x.IsBranch, n => n.Folder)
            .Add(x => x.SelectedKey, selectedKey)
            .Add(x => x.OnSelect, onSelect ?? (_ => { })));

    [Fact]
    public void Search_field_emits_every_keystroke()
    {
        string? value = null;
        var cut = Render<SearchField>(p => p.Add(x => x.Label, "Buscar").Add(x => x.ValueChanged, (string v) => value = v));
        cut.Find("input").Input("tab");
        value.Should().Be("tab");
    }

    [Fact]
    public void Text_field_label_wraps_its_input()
    {
        var cut = Render<TextField>(p => p.Add(x => x.Label, "Nombre"));
        cut.FindAll("label input").Should().HaveCount(1);
    }

    [Fact]
    public void Branches_start_collapsed()
    {
        var cut = RenderNodeTree();
        cut.Markup.Should().NotContain("Taberna");
    }

    [Fact]
    public void Clicking_a_branch_expands_it()
    {
        var cut = RenderNodeTree();
        cut.FindAll(".ds-list-row").Single(r => r.TextContent.Contains("Lugares")).Click();
        cut.Markup.Should().Contain("Taberna");
    }

    [Fact]
    public void Clicking_a_leaf_selects_it()
    {
        Node? selected = null;
        var cut = RenderNodeTree(onSelect: n => selected = n);
        cut.FindAll(".ds-list-row").Single(r => r.TextContent.Contains("Diario")).Click();
        selected!.Name.Should().Be("Diario");
    }

    [Fact]
    public void Selected_leaf_is_marked_with_aria_current()
    {
        var cut = RenderNodeTree(selectedKey: "Diario");
        cut.FindAll(".ds-list-row").Single(r => r.TextContent.Contains("Diario")).GetAttribute("aria-current").Should().Be("true");
    }

    [Fact]
    public void Leaf_row_has_no_aria_expanded()
    {
        var cut = RenderNodeTree();
        cut.FindAll(".ds-list-row").Single(r => r.TextContent.Contains("Diario")).HasAttribute("aria-expanded").Should().BeFalse();
    }

    [Fact]
    public void Branch_row_starts_with_aria_expanded_false()
    {
        var cut = RenderNodeTree();
        cut.FindAll(".ds-list-row").Single(r => r.TextContent.Contains("Lugares")).GetAttribute("aria-expanded").Should().Be("false");
    }

    [Fact]
    public void Clicking_a_branch_sets_aria_expanded_true()
    {
        var cut = RenderNodeTree();
        cut.FindAll(".ds-list-row").Single(r => r.TextContent.Contains("Lugares")).Click();
        cut.FindAll(".ds-list-row").Single(r => r.TextContent.Contains("Lugares")).GetAttribute("aria-expanded").Should().Be("true");
    }

    [Fact]
    public void Clicking_a_branch_does_not_invoke_on_select()
    {
        var called = false;
        var cut = RenderNodeTree(onSelect: _ => called = true);
        cut.FindAll(".ds-list-row").Single(r => r.TextContent.Contains("Lugares")).Click();
        called.Should().BeFalse();
    }
}
