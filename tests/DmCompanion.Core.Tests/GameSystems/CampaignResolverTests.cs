using DmCompanion.Core.GameSystems;
using DmCompanion.Core.Vault;

namespace DmCompanion.Core.Tests.GameSystems;

public class CampaignResolverTests
{
    private static VaultEntity Note(string path, string? type, string? system = null)
    {
        var fields = new Dictionary<string, object?>();
        if (type is not null) fields["type"] = type;
        if (system is not null) fields["system"] = system;
        return new VaultEntity(path, path, EntityTypes.Parse(type), fields, string.Empty, [], null, DateTime.UnixEpoch);
    }

    private static CampaignInfo Resolve(params VaultEntity[] entities) => CampaignResolver.Resolve(VaultIndex.Build(entities));

    [Fact]
    public void Without_campaign_the_system_is_dnd5e() =>
        Resolve(Note("a.md", "note")).SystemId.Should().Be("dnd5e");

    [Fact]
    public void Without_campaign_the_path_is_null() =>
        Resolve(Note("a.md", "note")).CampaignPath.Should().BeNull();

    [Fact]
    public void Campaign_declares_its_system() =>
        Resolve(Note("Campaña.md", "campaign", "cosmere")).SystemId.Should().Be("cosmere");

    [Fact]
    public void Shallower_campaign_wins() =>
        Resolve(Note("A/B/c.md", "campaign", "dnd5e"), Note("z.md", "campaign", "cosmere")).CampaignPath.Should().Be("z.md");

    [Fact]
    public void Same_depth_campaigns_are_ordered_ordinally() =>
        Resolve(Note("B/c.md", "campaign"), Note("A/c.md", "campaign")).CampaignPath.Should().Be("A/c.md");

    [Fact]
    public void Losing_campaigns_are_reported_as_discarded() =>
        Resolve(Note("B/c.md", "campaign"), Note("A/c.md", "campaign")).DiscardedPaths.Should().Equal("B/c.md");

    [Fact]
    public void Campaign_without_system_falls_back_to_dnd5e() =>
        Resolve(Note("c.md", "campaign")).SystemId.Should().Be("dnd5e");

    [Fact]
    public void Blank_system_falls_back_to_dnd5e() =>
        Resolve(Note("c.md", "campaign", "   ")).SystemId.Should().Be("dnd5e");
}
