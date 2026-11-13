using DmCompanion.Core.Vault;

namespace DmCompanion.Core.GameSystems;

public sealed record CampaignInfo(string SystemId, string? CampaignPath, IReadOnlyList<string> DiscardedPaths);

public static class CampaignResolver
{
    private const string DefaultSystemId = "dnd5e";

    public static CampaignInfo Resolve(VaultIndex index)
    {
        var campaigns = index.Entities
            .Where(entity => entity.Type == EntityType.Campaign)
            .OrderBy(entity => entity.RelativePath.Split('/').Length)
            .ThenBy(entity => entity.RelativePath, StringComparer.Ordinal)
            .ToList();
        if (campaigns.Count == 0) return new CampaignInfo(DefaultSystemId, null, []);

        var chosen = campaigns[0];
        var system = FrontmatterReader.GetString(chosen.Frontmatter, "system") ?? DefaultSystemId;
        return new CampaignInfo(system, chosen.RelativePath, campaigns.Skip(1).Select(entity => entity.RelativePath).ToList());
    }
}
