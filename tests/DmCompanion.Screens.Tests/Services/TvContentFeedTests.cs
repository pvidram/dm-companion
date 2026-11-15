using System.Text.RegularExpressions;
using DmCompanion.Screens.Services;

namespace DmCompanion.Screens.Tests;

public sealed class TvContentFeedTests : TvSessionTestBase
{
    private const string LighthouseNote =
        "---\ntype: location\n---\nFaro abandonado. Bajo él se abre la [[Mazmorra del Kraken]].\n\n![[Plano del Faro.png]]\n\n"
        + "%% El farero es un cultista. ![[suelto.png]] %%\n";

    private string Html => LastMessage.GetProperty("html").GetString()!;

    private async Task ShowLighthouseAsync()
    {
        await SaveNoteAsync(OldLighthouse, LighthouseNote);
        await Tv.ShowContentAsync(OldLighthouse);
    }

    [Fact]
    public async Task Show_publishes_content_rendered_for_players()
    {
        await ShowLighthouseAsync();

        (Tv.Mode, Tv.ContentPath, Tv.ContentTitle, LastKind, LastMessage.GetProperty("title").GetString(),
                Html.Contains("Faro abandonado"), Html.Contains("cultista"))
            .Should().Be((TvMode.Content, OldLighthouse, "Faro Viejo", "content", "Faro Viejo", true, false));
    }

    [Fact]
    public async Task Content_images_are_whitelisted_and_secret_images_are_not()
    {
        await ShowLighthouseAsync();

        var ids = Regex.Matches(Html, "src=\"files/(?<id>[^\"]+)\"").Select(m => m.Groups["id"].Value).ToList();
        (ids.Count, Server.Files.Keys.SequenceEqual(ids), Server.Files.Values.Single().ContentType).Should().Be((1, true, "image/png"));
    }

    [Fact]
    public async Task Links_render_as_plain_text_on_tv()
    {
        await ShowLighthouseAsync();

        (Html.Contains("<a"), Html.Contains("data-target"), Html.Contains("<span>Mazmorra del Kraken</span>"))
            .Should().Be((false, false, true));
    }

    [Fact]
    public async Task Show_ignores_notes_that_are_not_pc_or_location_or_are_secret()
    {
        await SaveNoteAsync("Lugares/Guarida.md", "---\ntype: location\nsecret: true\n---\nGuarida.\n");
        var published = Server.Published.Count;

        await Tv.ShowContentAsync("Personajes/Varek.md");
        await Tv.ShowContentAsync("Lugares/Guarida.md");
        await Tv.ShowContentAsync("No/Existe.md");

        (Tv.Mode, Server.Published.Count).Should().Be((TvMode.Waiting, published));
    }

    [Fact]
    public async Task Show_does_nothing_with_the_tv_off()
    {
        await Tv.DisableAsync();
        var published = Server.Published.Count;

        await Tv.ShowContentAsync("Personajes/Lyra.md");

        (Tv.Mode, Server.Published.Count).Should().Be((TvMode.Off, published));
    }

    [Fact]
    public async Task Pc_sheet_portrait_is_whitelisted()
    {
        await Tv.ShowContentAsync("Personajes/Lyra.md");

        var url = LastMessage.GetProperty("sheet").GetProperty("portraitUrl").GetString()!;
        (url.StartsWith("files/"), Server.Files.ContainsKey(url["files/".Length..])).Should().Be((true, true));
    }

    [Fact]
    public async Task Pc_without_portrait_has_null_portrait_url()
    {
        await Tv.ShowContentAsync("Personajes/Torvald.md");

        (LastMessage.GetProperty("sheet").GetProperty("portraitUrl").ValueKind, Server.Files.Count)
            .Should().Be((System.Text.Json.JsonValueKind.Null, 0));
    }

    [Fact]
    public async Task Pc_portrait_outside_the_vault_is_not_whitelisted()
    {
        await SaveNoteAsync("Personajes/Intruso.md", "---\ntype: pc\nname: Intruso\nportrait: ../fuera.png\n---\n");

        await Tv.ShowContentAsync("Personajes/Intruso.md");

        (LastMessage.GetProperty("sheet").GetProperty("portraitUrl").ValueKind, Server.Files.Count)
            .Should().Be((System.Text.Json.JsonValueKind.Null, 0));
    }

    [Fact]
    public async Task Location_notes_have_no_sheet()
    {
        await ShowLighthouseAsync();

        LastMessage.TryGetProperty("sheet", out _).Should().BeFalse();
    }

    [Fact]
    public async Task Image_added_after_the_note_edit_reaches_the_tv()
    {
        await ShowLighthouseAsync();
        await SaveNoteAsync(OldLighthouse, "---\ntype: location\n---\nFaro.\n\n![[faro-nuevo.png]]\n");
        await WaitUntilAsync(() => Html.Contains("wikilink broken"));

        File.Copy(Path.Combine(Host.Current!.RootPath, "Mapas", "suelto.png"), Path.Combine(Host.Current.RootPath, "Lugares", "faro-nuevo.png"));

        await WaitUntilAsync(() => Html.Contains("src=\"files/"));
    }

    [Fact]
    public async Task Back_to_map_republishes_last_map()
    {
        await FixOnCaveAsync();
        var map = Server.Published[^1];
        await ShowLighthouseAsync();

        await Tv.BackToMapAsync();

        (Tv.Mode, Tv.ContentPath, Server.Published[^1]).Should().Be((TvMode.Map, (string?)null, map));
    }

    [Fact]
    public async Task Back_to_map_without_a_fixed_map_goes_to_waiting()
    {
        await ShowLighthouseAsync();

        await Tv.BackToMapAsync();

        (Tv.Mode, LastKind).Should().Be((TvMode.Waiting, "waiting"));
    }

    [Fact]
    public async Task Edited_note_republishes_content()
    {
        await ShowLighthouseAsync();

        await SaveNoteAsync(OldLighthouse, "---\ntype: location\n---\nFaro en ruinas.\n");

        await WaitUntilAsync(() => LastKind == "content" && Html.Contains("Faro en ruinas"));
    }

    [Fact]
    public async Task Note_made_secret_goes_back_to_map()
    {
        await FixOnCaveAsync();
        await ShowLighthouseAsync();

        await SaveNoteAsync(OldLighthouse, "---\ntype: location\nsecret: true\n---\nFaro.\n");

        await WaitUntilAsync(() => Tv.Mode == TvMode.Map && LastKind == "map");
    }

    [Fact]
    public async Task Deleted_note_goes_back_to_map()
    {
        await FixOnCaveAsync();
        await ShowLighthouseAsync();

        await Host.Current!.DeleteAsync(OldLighthouse);

        await WaitUntilAsync(() => Tv.Mode == TvMode.Map && LastKind == "map" && Tv.ContentPath is null);
    }

    [Fact]
    public async Task Map_changes_while_showing_content_update_last_map_without_publishing()
    {
        await FixOnCaveAsync();
        await ShowLighthouseAsync();
        var published = Server.Published.Count;

        Maps.Mutate(e => e.MoveToken("t-bandido", 945, 665));

        await WaitUntilAsync(() => Tv.LastMap!.Json.Contains("\"x\":945"));
        (Server.Published.Count, LastKind).Should().Be((published, "content"));
    }

    [Fact]
    public async Task Back_to_map_publishes_the_latest_map()
    {
        await FixOnCaveAsync();
        await ShowLighthouseAsync();
        Maps.Mutate(e => e.MoveToken("t-bandido", 945, 665));
        await WaitUntilAsync(() => Tv.LastMap!.Json.Contains("\"x\":945"));

        await Tv.BackToMapAsync();

        TokenIn(LastMessage.GetProperty("map"), "t-bandido")!.Value.GetProperty("x").GetDouble().Should().Be(945);
    }

    [Fact]
    public async Task Leaving_the_tv_map_in_the_tablet_while_showing_content_keeps_it_for_back_to_map()
    {
        await FixOnCaveAsync();
        await ShowLighthouseAsync();

        await Maps.OpenAsync(Lighthouse);
        await Tv.BackToMapAsync();

        (Tv.Mode, Tv.MapPath, LastKind).Should().Be((TvMode.Map, Cave, "map"));
    }

    [Fact]
    public async Task Deleted_tv_map_while_showing_content_keeps_content_and_back_goes_to_waiting()
    {
        await FixOnCaveAsync();
        await ShowLighthouseAsync();

        await Library.DeleteAsync(Cave);
        await WaitUntilAsync(() => Tv.LastMap is null);
        var stillContent = (Tv.Mode, LastKind);
        await Tv.BackToMapAsync();

        (stillContent, Tv.Mode, LastKind).Should().Be(((TvMode.Content, "content"), TvMode.Waiting, "waiting"));
    }
}
