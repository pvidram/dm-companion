using DmCompanion.Core.Maps;
using DmCompanion.Core.Tv;

namespace DmCompanion.Screens.Services;

/// <summary>Objeto `map` del mensaje de la TV, compartido por el mensaje real y la vista previa de la tablet.</summary>
public static class PlayerMapJson
{
    /// <param name="urlFor">Convierte una ruta del vault en la URL que cita el mensaje (null si no procede).</param>
    /// <param name="wholeMap">Con true, <c>frame</c> es null y el dibujante usa el mapa entero.</param>
    public static object Map(PlayerMapView view, Func<string?, string?> urlFor, bool wholeMap) => new
    {
        imageUrl = urlFor(view.ImagePath),
        // Con imagen, la TV toma el tamaño de la propia imagen, como el lienzo de la tablet.
        width = view.ImagePath is null ? view.Width : 0,
        height = view.ImagePath is null ? view.Height : 0,
        grid = new { enabled = view.Grid.Enabled, cellSize = view.Grid.CellSize, offsetX = view.Grid.OffsetX, offsetY = view.Grid.OffsetY },
        frame = wholeMap ? null : new { x = view.Frame.X, y = view.Frame.Y, width = view.Frame.Width, height = view.Frame.Height },
        tokens = view.Tokens.Select(t => new
        {
            id = t.Id, name = t.Name, initial = t.Initial, portraitUrl = urlFor(t.PortraitPath),
            faction = TokenSideMapping.ToFaction(t.Side).ToString().ToLowerInvariant(),
            size = t.Size, x = t.X, y = t.Y, down = t.Down, current = t.Current, dead = t.Dead, conditions = t.Conditions,
        }).ToList(),
        strokes = view.Strokes.Select(s => new { id = s.Id, color = s.Color, width = s.Width, points = s.Points }).ToList(),
        fog = view.Fog,
        combat = view.Combat is { } combat
            ? new
            {
                round = combat.Round,
                entries = combat.Entries.Select(e => new
                {
                    tokenId = e.TokenId, name = e.Name, initiative = e.Initiative, current = e.Current,
                    dead = e.Dead, hp = e.Hp, conditions = e.Conditions,
                }).ToList(),
            }
            : null,
    };
}
