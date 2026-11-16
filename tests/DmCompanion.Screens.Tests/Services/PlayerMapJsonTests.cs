using System.Text.Json;
using DmCompanion.Core.Maps;
using DmCompanion.Core.Tv;
using DmCompanion.Screens.Services;

namespace DmCompanion.Screens.Tests;

public class PlayerMapJsonTests
{
    private static PlayerMapView View(IReadOnlyList<IReadOnlyList<double>> fog) =>
        new("mapas/cueva.md", "mapas/cueva.png", 2000, 1000, GridSettings.Default, new TvFrame(0, 0, 1600, 900),
            [new PlayerToken("t", "Lyra", "L", "retratos/lyra.png", TokenSide.Pc, 1, 10, 20, false, false, false, 0)], [], null, fog);

    private static string Serialize(PlayerMapView view, bool wholeMap) =>
        JsonSerializer.Serialize(PlayerMapJson.Map(view, p => p is null ? null : $"u/{p}", wholeMap), new JsonSerializerOptions(JsonSerializerDefaults.Web));

    [Fact]
    public void Fog_serializes_as_arrays_of_flat_numbers() =>
        Serialize(View([new List<double> { 0, 0, 100, 0, 100, 100, 0, 100 }]), false).Should().Contain("\"fog\":[[0,0,100,0,100,100,0,100]]");

    [Fact]
    public void Whole_map_serializes_null_frame() =>
        Serialize(View([]), true).Should().Contain("\"frame\":null");

    [Fact]
    public void Frame_is_serialized_when_not_whole_map() =>
        Serialize(View([]), false).Should().Contain("\"frame\":{\"x\":0,\"y\":0,\"width\":1600,\"height\":900}");

    [Fact]
    public void UrlFor_applies_to_image_and_portraits()
    {
        var json = Serialize(View([]), false);
        (json.Contains("\"imageUrl\":\"u/mapas/cueva.png\""), json.Contains("\"portraitUrl\":\"u/retratos/lyra.png\"")).Should().Be((true, true));
    }
}
