using DmCompanion.Core.GameSystems;
using DmCompanion.Core.Vault;

namespace DmCompanion.Core.Tests;

public class VaultScaffoldTests
{
    private static readonly IGameSystem Dnd5e = GameSystemRegistry.Default.Get("dnd5e");

    [Fact]
    public void Plan_builds_the_campaign_note_in_the_root()
    {
        var plan = VaultScaffold.Plan("Costa de Sal", Dnd5e)!;
        (plan.NoteRelativePath, plan.NoteContent).Should().Be(
            ("Costa de Sal.md", "---\ntype: campaign\nname: \"Costa de Sal\"\nsystem: dnd5e\n---\n## Resumen\n"));
    }

    [Fact]
    public void Plan_writes_the_id_of_the_chosen_system() =>
        VaultScaffold.Plan("X", GameSystemRegistry.Default.Get("cosmere"))!.NoteContent.Should().Contain("system: cosmere\n");

    [Fact]
    public void Plan_escapes_quotes_in_the_name_and_cleans_the_file_name()
    {
        var plan = VaultScaffold.Plan("Los \"Doce\"", Dnd5e)!;
        (plan.NoteRelativePath, plan.NoteContent).Should().Be(
            ("Los Doce.md", "---\ntype: campaign\nname: \"Los \\\"Doce\\\"\"\nsystem: dnd5e\n---\n## Resumen\n"));
    }

    [Fact]
    public void Plan_collapses_control_characters_in_the_name() =>
        VaultScaffold.Plan("Mi\ncampaña\t2", Dnd5e)!.NoteContent.Should().Contain("name: \"Mi campaña 2\"\n");

    [Theory]
    [InlineData("")] [InlineData("   ")] [InlineData("???")] [InlineData("///")]
    public void Plan_returns_null_for_names_with_nothing_usable(string name) =>
        VaultScaffold.Plan(name, Dnd5e).Should().BeNull();

    [Fact]
    public void Plan_app_json_is_an_empty_object() =>
        VaultScaffold.Plan("X", Dnd5e)!.AppJson.Should().Be("{}");
}
