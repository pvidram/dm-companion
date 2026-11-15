using System.Text.Json;
using System.Text.Json.Serialization;

namespace DmCompanion.Core.Maps;

public static class MapStateSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };

    /// <summary>Un JSON inválido o vacío es un resultado, nunca una excepción.</summary>
    public static MapStateReadResult Read(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Invalid();
        }

        try
        {
            var state = JsonSerializer.Deserialize<MapState>(json, Options);
            if (state is null || state.Version < 1)
            {
                return Invalid();
            }

            // Un fichero sin alguna sección se completa con valores vacíos.
            state = state with
            {
                Grid = state.Grid ?? GridSettings.Default,
                Tokens = state.Tokens ?? [],
                Strokes = state.Strokes ?? [],
            };
            if (!HasValidItems(state))
            {
                return Invalid();
            }

            if (state.Version > MapState.CurrentVersion)
            {
                return new MapStateReadResult(MapStateRepair.Normalize(state), MapStateReadStatus.FutureVersion);
            }

            var (repaired, repairs) = MapStateRepair.Repair(state with { Version = MapState.CurrentVersion });
            return new MapStateReadResult(repaired, MapStateReadStatus.Ok, repairs);
        }
        catch (JsonException)
        {
            return Invalid();
        }
    }

    private static bool HasValidItems(MapState state) =>
        IsValidGrid(state.Grid)
        && state.Tokens.All(token => token is { Id: not null, Name: not null, Size: >= 1 and <= 3, Ac: null or >= 0, Hp: null or >= 0, HpCurrent: null or >= 0 })
        && state.Strokes.All(stroke => stroke is { Id: not null, Color: not null, Points: not null } && stroke.Points.Count % 3 == 0);

    /// <summary>Tamaño de celda finito y positivo, desplazamientos finitos: con otra cosa el ajuste a la cuadrícula daría NaN.</summary>
    public static bool IsValidGrid(GridSettings grid) =>
        double.IsFinite(grid.CellSize) && grid.CellSize > 0 && double.IsFinite(grid.OffsetX) && double.IsFinite(grid.OffsetY);

    /// <summary>Redondea al escribir (posiciones a 1 decimal, presión a 2) para que el fichero no engorde con el ruido del lápiz.</summary>
    public static string Write(MapState state) => JsonSerializer.Serialize(Rounded(state), Options);

    private static MapState Rounded(MapState state) => state with
    {
        Tokens = [.. state.Tokens.Select(t => t with { X = Round(t.X, 1), Y = Round(t.Y, 1) })],
        Strokes = [.. state.Strokes.Select(s => s with { Points = [.. s.Points.Select((v, i) => Round(v, i % 3 == 2 ? 2 : 1))] })],
    };

    private static double Round(double value, int decimals) => Math.Round(value, decimals, MidpointRounding.AwayFromZero);

    private static MapStateReadResult Invalid() => new(MapState.Empty, MapStateReadStatus.Invalid);
}
