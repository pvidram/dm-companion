using DmCompanion.Core.Glossary;
using Microsoft.Extensions.Logging.Abstractions;
using DmCompanion.Core.Vault;

namespace DmCompanion.Core.Tests.Glossary;

public sealed class TokenSourceResolverTests
{
    private static readonly GlossaryPackLoader Loader = new(new FixturePackSource(), NullLogger<GlossaryPackLoader>.Instance);
    private static readonly VaultEntity Note = VaultEntityFactory.Create("Personajes/Ana.md", "---\ntype: npc\nname: Ana\n---\n", DateTime.UtcNow);
    private static readonly VaultIndex Vault = VaultIndex.Build([Note]);

    private static TokenSource Resolve(string? source) => TokenSourceResolver.Resolve(source, Vault, Loader.Get);

    [Fact]
    public void Null_source_is_generic() => Resolve(null).Kind.Should().Be(TokenSourceKind.Generic);

    [Fact]
    public void Srd_key_resolves_to_entry()
    {
        var source = Resolve("srd:criaturas/aboleth");
        (source.Kind, source.Entry?.Name).Should().Be((TokenSourceKind.Srd, "Aboleth"));
    }

    [Fact]
    public void Unknown_srd_key_is_missing() => Resolve("srd:criaturas/nope").Kind.Should().Be(TokenSourceKind.Missing);

    [Fact]
    public void Vault_path_resolves_to_note()
    {
        var source = Resolve("Personajes/Ana.md");
        (source.Kind, source.Note).Should().Be((TokenSourceKind.VaultNote, Note));
    }

    [Fact]
    public void Missing_vault_path_is_missing() => Resolve("Personajes/Nadie.md").Kind.Should().Be(TokenSourceKind.Missing);

    [Fact]
    public void Srd_creature_exposes_adapted_frontmatter()
        => Resolve("srd:criaturas/aboleth").Frontmatter.Should().ContainKey("stats");

    [Fact]
    public void Srd_non_creature_has_no_frontmatter()
        => Resolve("srd:estados/blinded").Frontmatter.Should().BeNull();
}
