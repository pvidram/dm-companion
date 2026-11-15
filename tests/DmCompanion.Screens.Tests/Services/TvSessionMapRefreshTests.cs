using DmCompanion.Screens.Services;

namespace DmCompanion.Screens.Tests;

/// <summary>Con el mapa de la TV cerrado en la tablet, los cambios del vault que afectan a su vista se vuelven a proyectar.</summary>
public sealed class TvSessionMapRefreshTests : TvSessionTestBase
{
    private string? MapImageUrl => LastMessage.GetProperty("map").GetProperty("imageUrl").GetString();

    private string? LyraPortraitUrl => TokenIn(LastMessage.GetProperty("map"), "t-lyra")!.Value.GetProperty("portraitUrl").GetString();

    private async Task FixOnCaveThenOpenAnotherAsync()
    {
        await FixOnCaveAsync();
        await Maps.OpenAsync(Lighthouse);
    }

    [Fact]
    public async Task Map_note_image_change_reprojects_the_tv_map()
    {
        await FixOnCaveThenOpenAnotherAsync();
        MapImageUrl.Should().BeNull();

        await SaveNoteAsync(Cave, "---\ntype: map\nname: Boceto de la cueva\nimage: Mapas/suelto.png\n---\n");

        await WaitUntilAsync(() => LastKind == "map" && MapImageUrl is { } url && Server.Files.ContainsKey(url["files/".Length..]));
    }

    [Fact]
    public async Task Token_source_portrait_change_reprojects_the_tv_map()
    {
        await FixOnCaveThenOpenAnotherAsync();
        var before = LyraPortraitUrl;

        await SaveNoteAsync("Personajes/Lyra.md", "---\ntype: pc\nname: Lyra Vientoalto\nportrait: _assets/lyra-nueva.png\n---\n");

        await WaitUntilAsync(() => LyraPortraitUrl is { } url && url != before);
    }

    [Fact]
    public async Task Map_note_change_while_showing_content_only_updates_last_map()
    {
        await FixOnCaveThenOpenAnotherAsync();
        await Tv.ShowContentAsync("Personajes/Lyra.md");
        var published = Server.Published.Count;

        await SaveNoteAsync(Cave, "---\ntype: map\nname: Boceto de la cueva\nimage: Mapas/suelto.png\n---\n");

        await WaitUntilAsync(() => Tv.LastMap!.Json.Contains("\"imageUrl\":\"files/"));
        (Server.Published.Count, LastKind).Should().Be((published, "content"));
    }

    [Fact]
    public async Task Replacing_the_map_image_at_the_same_path_sends_a_new_url()
    {
        await Maps.OpenAsync(Lighthouse);
        await Tv.FixFrameAsync(Lighthouse, Square);
        var before = MapImageUrl;
        var png = VaultPathOf("Mapas/Plano del Faro.png");

        File.WriteAllBytes(png, [.. File.ReadAllBytes(png), 0]);
        File.SetLastWriteTimeUtc(png, DateTime.UtcNow.AddMinutes(5));

        await WaitUntilAsync(() => LastKind == "map" && MapImageUrl is { } url && url != before && Server.Files.ContainsKey(url["files/".Length..]));
    }
}
