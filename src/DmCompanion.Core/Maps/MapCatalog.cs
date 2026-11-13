using DmCompanion.Core.Vault;

namespace DmCompanion.Core.Maps;

/// <summary>Mapas del vault e imágenes de `Mapas/` que ningún mapa referencia.</summary>
public sealed record MapCatalog(IReadOnlyList<MapDefinition> Maps, IReadOnlyList<string> OrphanImages)
{
    private const string MapsPrefix = MapPaths.MapsFolder + "/";

    public static MapCatalog Build(VaultIndex index, IEnumerable<string> imagePaths)
    {
        var maps = index.OfType(EntityType.Map)
            .Select(MapDefinition.FromEntity)
            .OfType<MapDefinition>()
            .OrderBy(map => map.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var referenced = maps
            .Where(map => map.ImagePath is not null)
            .Select(map => VaultPaths.Normalize(map.ImagePath!))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var orphans = imagePaths
            .Select(VaultPaths.Normalize)
            .Where(path => path.StartsWith(MapsPrefix, StringComparison.OrdinalIgnoreCase))
            .Where(path => MapPaths.IsImage(path) && !VaultPaths.IsConflictFile(path))
            .Where(path => !referenced.Contains(path))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new MapCatalog(maps, orphans);
    }
}
