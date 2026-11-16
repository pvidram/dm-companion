using DmCompanion.Core.Glossary;

namespace DmCompanion.Core.Tests.Glossary;

public sealed class CreatureTokenSizeTests
{
    [Fact]
    public void Medium_is_1() => CreatureTokenSize.Of(["Medium"]).Should().Be(1);

    [Fact]
    public void Large_is_2() => CreatureTokenSize.Of(["Large"]).Should().Be(2);

    [Fact]
    public void Huge_is_3() => CreatureTokenSize.Of(["Huge"]).Should().Be(3);

    [Fact]
    public void Gargantuan_is_3() => CreatureTokenSize.Of(["Gargantuan"]).Should().Be(3);

    [Fact]
    public void Tiny_is_1() => CreatureTokenSize.Of(["Tiny"]).Should().Be(1);

    [Fact]
    public void The_first_size_wins() => CreatureTokenSize.Of(["Small", "Medium"]).Should().Be(1);
}
