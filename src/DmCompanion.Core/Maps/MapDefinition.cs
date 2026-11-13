using System.Globalization;
using DmCompanion.Core.Vault;

namespace DmCompanion.Core.Maps;

/// <summary>
/// Mapa descrito por una nota `type: map`. Con imagen, Width y Height son 0: las dimensiones reales
/// las aporta el canvas al cargarla. Sin imagen es un lienzo en blanco de tamaño `canvas` (o el de por defecto).
/// </summary>
public sealed record MapDefinition(string NotePath, string Name, string? ImagePath, double Width, double Height)
{
    public const double DefaultCanvasWidth = 2800;
    public const double DefaultCanvasHeight = 2100;

    public string StatePath => MapPaths.StatePathFor(NotePath);

    public static MapDefinition? FromEntity(VaultEntity entity)
    {
        if (entity.Type != EntityType.Map)
        {
            return null;
        }

        var image = FrontmatterReader.GetString(entity.Frontmatter, "image");
        if (image is not null)
        {
            return new MapDefinition(entity.RelativePath, entity.Title, VaultPaths.Normalize(image), 0, 0);
        }

        var canvas = FrontmatterReader.GetMap(entity.Frontmatter, "canvas");
        return new MapDefinition(
            entity.RelativePath,
            entity.Title,
            null,
            ReadSize(canvas, "width", DefaultCanvasWidth),
            ReadSize(canvas, "height", DefaultCanvasHeight));
    }

    private static double ReadSize(IReadOnlyDictionary<string, object?> canvas, string key, double fallback) =>
        FrontmatterReader.GetString(canvas, key) is { } text
        && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
        && value > 0
            ? value
            : fallback;
}
