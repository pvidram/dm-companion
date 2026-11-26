using System.Text.Json;
using DmCompanion.Core.Journal;
using DmCompanion.Screens.Services;

namespace DmCompanion.Screens.Tests;

public sealed class TvJournalTests : TvSessionTestBase
{
    private const string Diary = "Personajes/Lyra/Diario";

    private void WriteImage(string name, params byte[] bytes)
    {
        Directory.CreateDirectory(VaultPathOf(Diary));
        File.WriteAllBytes(VaultPathOf($"{Diary}/{name}"), bytes.Length == 0 ? [1, 2, 3] : bytes);
    }

    // La entrada se lee del texto, como lo hace JournalIndex; `exists` simula imágenes que faltan.
    private static JournalEntry EntryOf(string title, string added, string[] images, string note = "", Func<string, bool>? exists = null) =>
        JournalReader.Read($"{Diary}/{title}.md",
            $"---\ntype: journal\nadded: {added}\nimages:\n{string.Concat(images.Select(name => $"  - \"{name}\"\n"))}---\n{note}\n",
            DateTime.UtcNow, exists ?? (_ => true));

    private JournalEntry Ankheg(string note = "")
    {
        WriteImage("2026-11-16 Ankheg-1.png", 1, 1, 1);
        WriteImage("2026-11-16 Ankheg-2.jpg", 2, 2, 2);
        return EntryOf("2026-11-16 Ankheg", "2026-11-16T21:34:00+01:00", ["2026-11-16 Ankheg-1.png", "2026-11-16 Ankheg-2.jpg"], note);
    }

    private string Caption => LastMessage.GetProperty("caption").GetString()!;

    [Fact]
    public async Task Showing_an_entry_publishes_a_slide_message_with_its_first_image()
    {
        await Tv.ShowJournalEntryAsync(Ankheg());

        (Tv.Mode, LastKind, Caption, LastMessage.GetProperty("image").GetString()!.StartsWith("files/", StringComparison.Ordinal))
            .Should().Be((TvMode.Content, "slide", "Lyra · Ankheg · imagen 1 de 2", true));
    }

    [Fact]
    public async Task A_slide_message_has_no_text_when_it_is_an_image()
    {
        await Tv.ShowJournalEntryAsync(Ankheg("Una nota"));

        LastMessage.GetProperty("text").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task The_image_of_the_slide_is_the_only_whitelisted_file_and_it_is_the_diary_image()
    {
        await Tv.ShowJournalEntryAsync(Ankheg());

        await using var stream = await Server.Files.Values.Single().Open(CancellationToken.None);
        var bytes = new byte[3];
        await stream.ReadExactlyAsync(bytes);
        (Server.Files.Count, Server.Files.Values.Single().ContentType, Convert.ToHexString(bytes)).Should().Be((1, "image/png", "010101"));
    }

    [Fact]
    public async Task Next_moves_to_the_next_image_and_whitelists_only_that_one()
    {
        await Tv.ShowJournalEntryAsync(Ankheg());

        await Tv.JournalNextAsync();

        (Caption, Server.Files.Count, Server.Files.Values.Single().ContentType, Tv.Journal!.Position).Should().Be(("Lyra · Ankheg · imagen 2 de 2", 1, "image/jpeg", 1));
    }

    [Fact]
    public async Task Previous_goes_back()
    {
        await Tv.ShowJournalEntryAsync(Ankheg());
        await Tv.JournalNextAsync();

        await Tv.JournalPreviousAsync();

        (Caption, Tv.Journal!.Position).Should().Be(("Lyra · Ankheg · imagen 1 de 2", 0));
    }

    [Fact]
    public async Task Moving_past_either_end_does_nothing()
    {
        await Tv.ShowJournalEntryAsync(Ankheg());
        await Tv.JournalPreviousAsync();
        await Tv.JournalNextAsync();
        var published = Server.Published.Count;

        await Tv.JournalNextAsync();
        await Tv.JournalNextAsync();
        await Tv.JournalPreviousAsync();
        await Tv.JournalPreviousAsync();
        await Tv.JournalPreviousAsync();

        (Server.Published.Count, Tv.Journal!.Position).Should().Be((published + 1, 0));
    }

    [Fact]
    public async Task The_last_page_of_an_entry_with_a_note_is_the_note_as_text()
    {
        await Tv.ShowJournalEntryAsync(Ankheg("Salió del suelo."));
        await Tv.JournalNextAsync();

        await Tv.JournalNextAsync();

        (Caption, LastMessage.GetProperty("text").GetString(), LastMessage.GetProperty("image").ValueKind, Server.Files.Count)
            .Should().Be(("Lyra · Ankheg", "Salió del suelo.", JsonValueKind.Null, 0));
    }

    [Fact]
    public async Task A_missing_image_and_a_heic_are_skipped()
    {
        WriteImage("buena.png");
        WriteImage("foto.heic");
        var entry = EntryOf("2026-11-16 Mixta", "2026-11-16T21:34:00+01:00", ["falta.png", "foto.heic", "buena.png"], exists: path => !path.EndsWith("falta.png", StringComparison.Ordinal));

        await Tv.ShowJournalEntryAsync(entry);

        (Caption, Tv.Journal!.Slides.Count).Should().Be(("Lyra · Mixta · imagen 1 de 1", 1));
    }

    [Fact]
    public async Task Next_skips_an_image_that_was_deleted_after_the_slides_were_taken()
    {
        WriteImage("2026-11-16 Tres-3.png", 3, 3, 3);
        var entry = Ankheg();
        entry = entry with { Images = [.. entry.Images, new JournalImage("2026-11-16 Tres-3.png", $"{Diary}/2026-11-16 Tres-3.png", true, true)] };
        await Tv.ShowJournalEntryAsync(entry);
        await Host.Current!.DeleteAsync($"{Diary}/2026-11-16 Ankheg-2.jpg");
        await Task.Delay(300);

        await Tv.JournalNextAsync();

        (Caption, Tv.Journal!.Position).Should().Be(("Lyra · Ankheg · imagen 3 de 3", 2));
    }

    [Fact]
    public async Task When_every_image_of_the_entry_is_gone_moving_sends_the_tv_back_to_the_map()
    {
        await Tv.ShowJournalEntryAsync(Ankheg());
        await Host.Current!.DeleteAsync($"{Diary}/2026-11-16 Ankheg-1.png");
        await Host.Current!.DeleteAsync($"{Diary}/2026-11-16 Ankheg-2.jpg");
        await Task.Delay(300);

        await Tv.JournalNextAsync();

        (Tv.Mode, Tv.Journal).Should().Be((TvMode.Waiting, null));
    }

    [Fact]
    public async Task An_entry_with_nothing_to_show_changes_nothing()
    {
        var published = Server.Published.Count;
        var entry = EntryOf("2026-11-16 Vacia", "2026-11-16T21:34:00+01:00", ["falta.png"], exists: _ => false);

        await Tv.ShowJournalEntryAsync(entry);

        (Tv.Mode, Tv.Journal, Server.Published.Count).Should().Be((TvMode.Waiting, null, published));
    }

    [Fact]
    public async Task With_the_tv_off_nothing_is_shown()
    {
        await Tv.DisableAsync();
        var entry = Ankheg();

        await Tv.ShowJournalEntryAsync(entry);

        (Tv.Mode, Tv.Journal).Should().Be((TvMode.Off, null));
    }

    [Fact]
    public async Task While_showing_an_entry_the_tv_reports_its_title_and_no_note_path()
    {
        await Tv.ShowJournalEntryAsync(Ankheg());

        (Tv.ContentTitle, Tv.ContentPath, Tv.Journal!.IsLog).Should().Be(("Lyra · Ankheg", null, false));
    }

    [Fact]
    public async Task Back_to_map_leaves_the_slides()
    {
        await Tv.ShowJournalEntryAsync(Ankheg());

        await Tv.BackToMapAsync();

        (Tv.Mode, Tv.Journal, LastKind).Should().Be((TvMode.Waiting, null, "waiting"));
    }

    [Fact]
    public async Task Showing_another_content_replaces_the_slides()
    {
        await Tv.ShowJournalEntryAsync(Ankheg());

        await Tv.ShowContentAsync("Personajes/Lyra.md");

        (Tv.Journal, LastKind).Should().Be((null, "content"));
    }

    [Fact]
    public async Task An_edit_of_a_note_does_not_move_or_republish_the_slides()
    {
        await Tv.ShowJournalEntryAsync(Ankheg());
        await Tv.JournalNextAsync();
        var published = Server.Published.Count;

        await SaveNoteAsync("Lugares/Faro Viejo.md", "---\ntype: location\n---\nOtro texto.\n");
        await Task.Delay(300);

        (Tv.Mode, Tv.Journal!.Position, Server.Published.Count).Should().Be((TvMode.Content, 1, published));
    }

    [Fact]
    public async Task Changing_the_vault_sends_the_tv_back_to_waiting()
    {
        using var other = TempVault.Empty();
        await Tv.ShowJournalEntryAsync(Ankheg());

        await Host.OpenAsync(other.Root);

        await WaitUntilAsync(() => Tv.Mode == TvMode.Waiting);
        Tv.Journal.Should().BeNull();
    }

    [Fact]
    public async Task The_log_plays_every_entry_in_log_order_image_by_image()
    {
        WriteImage("v.png");
        WriteImage("n1.png");
        WriteImage("n2.png");
        var old = EntryOf("2026-11-10 Vieja", "2026-11-10T20:00:00+01:00", ["v.png"], "No sale en la bitácora");
        var recent = EntryOf("2026-11-16 Nueva", "2026-11-16T21:34:00+01:00", ["n1.png", "n2.png"]);

        await Tv.ShowJournalLogAsync([recent, old]);
        var first = Caption;
        await Tv.JournalNextAsync();
        var second = Caption;
        await Tv.JournalNextAsync();

        (first, second, Caption, Tv.ContentTitle, Tv.Journal!.IsLog, Tv.Journal.Current.BarLabel(true), LastMessage.GetProperty("text").ValueKind)
            .Should().Be(("Lyra · Vieja · imagen 1 de 1", "Lyra · Nueva · imagen 1 de 2", "Lyra · Nueva · imagen 2 de 2", "Bitácora", true, "Entrada 2 de 2", JsonValueKind.Null));
    }

    [Fact]
    public async Task The_log_of_entries_without_images_changes_nothing()
    {
        await Tv.ShowJournalLogAsync([]);

        (Tv.Mode, Tv.Journal).Should().Be((TvMode.Waiting, null));
    }
}
