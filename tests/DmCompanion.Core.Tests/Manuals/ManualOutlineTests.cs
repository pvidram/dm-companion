using DmCompanion.Core.Manuals;

namespace DmCompanion.Core.Tests.Manuals;

public class ManualOutlineTests
{
    private static IReadOnlyList<OutlineItem> Sample() => ManualOutline.Build(
    [
        new("Capítulo 1", 3, 0),
        new("Razas", 5, 1),
        new("Elfos", 6, 2),
        new("Capítulo 2", 20, 0),
    ]);

    [Fact]
    public void Flat_levels_become_a_tree()
    {
        var tree = Sample();
        tree.Select(n => n.Title).Should().Equal("Capítulo 1", "Capítulo 2");
        tree[0].Children.Single().Children.Single().Title.Should().Be("Elfos");
    }

    [Fact]
    public void Level_jump_attaches_to_last_node()
    {
        var tree = ManualOutline.Build([new("A", 1, 0), new("B", null, 3)]);
        tree.Single().Children.Single().Page.Should().BeNull();
    }

    [Fact]
    public void Filter_keeps_ancestors()
    {
        var tree = ManualOutline.Filter(Sample(), "elfos");
        tree.Single().Children.Single().Children.Single().Title.Should().Be("Elfos");
    }

    [Fact]
    public void Filter_ignores_accents_and_case() =>
        ManualOutline.Filter(Sample(), "CAPITULO 2").Single().Title.Should().Be("Capítulo 2");

    [Fact]
    public void Empty_filter_returns_all() =>
        ManualOutline.Filter(Sample(), "  ").Should().HaveCount(2);

    [Theory]
    [InlineData(2, null)]
    [InlineData(5, "Razas")]
    [InlineData(7, "Elfos")]
    [InlineData(25, "Capítulo 2")]
    public void SectionAt_returns_last_section_starting_at_or_before_page(int page, string? expected) =>
        ManualOutline.SectionAt(Sample(), page).Should().Be(expected);

    [Fact]
    public void Matching_node_keeps_its_children() =>
        ManualOutline.Filter(Sample(), "razas").Single().Children.Single().Children.Single().Title.Should().Be("Elfos");

    [Fact]
    public void Match_in_intermediate_node_keeps_parents_and_children()
    {
        var tree = ManualOutline.Filter(Sample(), "razas");
        (tree.Single().Title, tree.Single().Children.Single().Title).Should().Be(("Capítulo 1", "Razas"));
    }
}
