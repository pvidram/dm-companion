using DmCompanion.Core.Vault;

namespace DmCompanion.Core.Tests;

public class VaultIndexTests
{
    private static VaultEntity Note(string path, string content = "") =>
        VaultEntityFactory.Create(path, content, DateTime.UnixEpoch);

    private static readonly VaultIndex Sample = VaultIndex.Build([
        Note("Lugares/Puerto Sal.md", "---\ntype: location\n---\nPuerto del [[Faro Viejo]]."),
        Note("Lugares/Faro Viejo.md", "---\ntype: location\n---\nCerca de [[Puerto Sal]]."),
        Note("Personajes/Varek.md", "---\ntype: npc\nname: Varek el Tuerto\n---\nVive en [[puerto sal]] y [[Puerto Sal|el puerto]]."),
        Note("Personajes/Lyra.md", "---\ntype: pc\n---\nBusca la campaña perdida. [[Inexistente]]"),
        Note("Archivo/Viejo/Varek.md", "Duplicado más profundo"),
    ]);

    [Fact]
    public void Get_finds_entity_by_path_ignoring_case() =>
        Sample.Get("lugares/puerto sal.md")!.RelativePath.Should().Be("Lugares/Puerto Sal.md");

    [Fact]
    public void Resolve_matches_file_name_ignoring_case() =>
        Sample.Resolve("PUERTO SAL")!.RelativePath.Should().Be("Lugares/Puerto Sal.md");

    [Fact]
    public void Resolve_uses_file_name_not_title() =>
        Sample.Resolve("Varek el Tuerto").Should().BeNull();

    [Fact]
    public void Resolve_prefers_shortest_path_on_duplicates() =>
        Sample.Resolve("Varek")!.RelativePath.Should().Be("Personajes/Varek.md");

    [Fact]
    public void Resolve_accepts_paths_and_extension() =>
        Sample.Resolve("Viejo/Varek.md")!.RelativePath.Should().Be("Archivo/Viejo/Varek.md");

    [Fact]
    public void IsBroken_detects_missing_targets() =>
        Sample.IsBroken(new Wikilink("Inexistente", null, null, false)).Should().BeTrue();

    [Fact]
    public void GetBacklinks_lists_each_source_once() =>
        Sample.GetBacklinks("Lugares/Puerto Sal.md").Select(e => e.RelativePath)
            .Should().BeEquivalentTo(["Lugares/Faro Viejo.md", "Personajes/Varek.md"]);

    [Fact]
    public void GetBacklinks_is_empty_for_unlinked_notes() =>
        Sample.GetBacklinks("Personajes/Lyra.md").Should().BeEmpty();

    [Fact]
    public void OfType_filters_by_entity_type() =>
        Sample.OfType(EntityType.Location).Should().HaveCount(2);

    [Fact]
    public void Search_ranks_title_matches_before_body_matches() =>
        Sample.Search("puerto").Select(e => e.RelativePath).Should().Equal(
            "Lugares/Puerto Sal.md", "Lugares/Faro Viejo.md", "Personajes/Varek.md");

    [Fact]
    public void Search_ignores_accents() =>
        Sample.Search("campana").Single().RelativePath.Should().Be("Personajes/Lyra.md");

    [Fact]
    public void Search_with_blank_query_returns_nothing() =>
        Sample.Search("  ").Should().BeEmpty();

    [Fact]
    public void Upsert_replaces_existing_entity()
    {
        var updated = Sample.Upsert(Note("Personajes/Lyra.md", "---\nname: Lyra Vientoalto\n---\n"));
        (updated.Get("Personajes/Lyra.md")!.Title, updated.Count).Should().Be(("Lyra Vientoalto", Sample.Count));
    }

    [Fact]
    public void Upsert_updates_backlinks()
    {
        var updated = Sample.Upsert(Note("Personajes/Lyra.md", "Visita [[Faro Viejo]]"));
        updated.GetBacklinks("Lugares/Faro Viejo.md").Select(e => e.RelativePath).Should().Contain("Personajes/Lyra.md");
    }

    [Fact]
    public void Remove_drops_entity_and_its_backlinks()
    {
        var updated = Sample.Remove("Personajes/Varek.md");
        updated.GetBacklinks("Lugares/Puerto Sal.md").Select(e => e.RelativePath).Should().Equal("Lugares/Faro Viejo.md");
    }

    [Fact]
    public void Upsert_does_not_modify_original_index()
    {
        _ = Sample.Remove("Personajes/Lyra.md");
        Sample.Get("Personajes/Lyra.md").Should().NotBeNull();
    }
}
