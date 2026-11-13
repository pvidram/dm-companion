using DmCompanion.Core.Maps;

namespace DmCompanion.Core.Tests;

public class TokenNamingTests
{
    [Fact]
    public void Next_returns_the_base_name_when_free() =>
        TokenNaming.Next("Goblin", []).Should().Be("Goblin");

    [Fact]
    public void Next_adds_2_when_the_base_name_is_taken() =>
        TokenNaming.Next("Goblin", ["Goblin"]).Should().Be("Goblin 2");

    [Fact]
    public void Next_uses_the_first_free_number() =>
        TokenNaming.Next("Goblin", ["Goblin", "Goblin 3"]).Should().Be("Goblin 2");

    [Fact]
    public void Next_ignores_case() =>
        TokenNaming.Next("Goblin", ["goblin"]).Should().Be("Goblin 2");
}
