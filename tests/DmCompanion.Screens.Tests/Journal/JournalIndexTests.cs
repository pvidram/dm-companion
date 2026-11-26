using DmCompanion.Core.Journal;
using DmCompanion.Core.Vault.Storage;
using DmCompanion.Screens.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DmCompanion.Screens.Tests;

public sealed class JournalIndexTests : JournalUiTestBase
{
    private const string Ankheg = LyraDiary + "/2026-11-16 Ankheg.md";
    private const string Lobo = LyraDiary + "/2026-11-10 Lobo.md";
    private const string Faro = TorvaldDiary + "/2026-11-12 Faro.md";
    private const string Rota = TorvaldDiary + "/2026-11-13 Rota.md";
    private const string Raro = "Otros/Nadie/Diario/2026-11-01 Raro.md";

    protected override Task SeedAsync()
    {
        WriteFile(Ankheg, EntryText("2026-11-16T21:34:00+01:00", ["2026-11-16 Ankheg-1.jpg", "2026-11-16 Ankheg-2.jpg"], "srd:criaturas/ankheg", "Salió del suelo."));
        WriteImage(LyraDiary + "/2026-11-16 Ankheg-1.jpg");
        WriteImage(LyraDiary + "/2026-11-16 Ankheg-2.jpg");
        WriteFile(Lobo, EntryText("2026-11-10T20:00:00+01:00", ["2026-11-10 Lobo-1.png"], "[[Lobo]]"));
        WriteImage(LyraDiary + "/2026-11-10 Lobo-1.png");
        WriteFile("Monstruos/Lobo.md", "---\ntype: monster\nname: Lobo\n---\nLobo.\n");
        WriteFile(Faro, EntryText("2026-11-12T19:00:00+01:00", ["falta.png"]));
        WriteFile(Rota, "---\ntype: journal\nimages: [sin cerrar\n---\nHola\n");
        File.SetLastWriteTimeUtc(Vault.PathOf(Rota), new DateTime(2026, 11, 13, 12, 0, 0, DateTimeKind.Utc));
        WriteFile(LyraDiary + "/Suelta.md", "---\ntype: note\n---\nNo es una entrada.\n");
        WriteFile(Raro, EntryText("2026-11-01T10:00:00+01:00", ["2026-11-01 Raro-1.png"]));
        WriteImage("Otros/Nadie/Diario/2026-11-01 Raro-1.png");
        return Task.CompletedTask;
    }

    private static JournalEntry EntryWith(string? creature) =>
        JournalReader.Read(Ankheg, EntryText("2026-11-16T21:34:00+01:00", ["a.png"], creature), DateTime.UtcNow, _ => true);

    [Fact]
    public void Entries_are_in_log_order_oldest_first() =>
        string.Join(",", Journal.Entries.Select(e => e.Title)).Should().Be("Raro,Lobo,Faro,Rota,Ankheg");

    [Fact]
    public void A_note_in_Diario_without_type_journal_is_not_an_entry() =>
        Journal.Get(LyraDiary + "/Suelta.md").Should().BeNull();

    [Fact]
    public void An_entry_with_invalid_yaml_is_listed_with_a_warning()
    {
        var entry = Journal.Get(Rota)!;

        (entry.Warnings.Count > 0, entry.Warnings[0].StartsWith("Frontmatter YAML inválido", StringComparison.Ordinal), entry.Note)
            .Should().Be((true, true, "Hola"));
    }

    [Fact]
    public void ForPc_lists_the_entries_of_that_diary_newest_first() =>
        string.Join(",", Journal.ForPc(Lyra).Select(e => e.Title)).Should().Be("Ankheg,Lobo");

    [Fact]
    public void ForPc_uses_the_diary_folder_named_after_the_pc_note() =>
        string.Join(",", Journal.ForPc(Torvald).Select(e => e.Title)).Should().Be("Rota,Faro");

    [Fact]
    public void An_entry_whose_folder_is_no_pc_is_listed_but_has_no_owner()
    {
        var entry = Journal.Get(Raro)!;

        (entry.OwnerName, Journal.OwnerPcPathOf(entry), Journal.ForPc(Lyra).Contains(entry)).Should().Be(("Nadie", null, false));
    }

    [Fact]
    public void The_owner_of_an_entry_is_the_pc_of_its_diary_folder() =>
        Journal.OwnerPcPathOf(Journal.Get(Ankheg)!).Should().Be(Lyra);

    [Fact]
    public async Task Listed_images_exist_after_the_images_are_listed()
    {
        await Journal.RefreshImagesAsync();

        Journal.Get(Ankheg)!.Images.All(i => i.Exists).Should().BeTrue();
    }

    [Fact]
    public async Task A_missing_image_is_flagged_once_the_images_are_listed()
    {
        await Journal.RefreshImagesAsync();

        Journal.Get(Faro)!.Images[0].Exists.Should().BeFalse();
    }

    [Fact]
    public async Task Images_are_taken_as_existing_until_they_are_listed()
    {
        // Antes del primer listado nada se marca como perdido: evita el parpadeo de «Imagen no encontrada» tras guardar.
        var first = Journal.Get(Faro)!.Images[0].Exists;
        await WaitUntilAsync(() => !Journal.Get(Faro)!.Images[0].Exists);

        first.Should().BeTrue();
    }

    [Fact]
    public async Task An_image_that_arrives_later_stops_being_missing_and_raises_Changed()
    {
        await Journal.RefreshImagesAsync();
        var raised = 0;
        Journal.Changed += (_, _) => Interlocked.Increment(ref raised);

        WriteImage(TorvaldDiary + "/falta.png");

        // La URL con la fecha real del fichero solo sale de un relistado, no del modo optimista de mientras se lista («?v=0»).
        var expected = $"?v={File.GetLastWriteTimeUtc(Vault.PathOf(TorvaldDiary + "/falta.png")).Ticks}";
        await WaitUntilAsync(() => Journal.ImageUrl(Journal.Get(Faro)!.Images[0])?.EndsWith(expected, StringComparison.Ordinal) == true);
        raised.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task A_note_saved_through_the_vault_shows_up_without_waiting_for_the_watcher()
    {
        var before = Journal.Entries.Count;

        await Host.Current!.SaveAsync(TorvaldDiary + "/2026-11-17 Nueva.md", EntryText("2026-11-17T10:00:00+01:00", ["x.png"]), WritePrecondition.MustNotExist);

        Journal.Entries.Count.Should().Be(before + 1);
    }

    [Fact]
    public async Task A_note_deleted_through_the_vault_leaves_the_index()
    {
        await Host.Current!.DeleteAsync(Lobo);

        Journal.Get(Lobo).Should().BeNull();
    }

    [Fact]
    public async Task Opening_another_vault_drops_the_entries_of_the_previous_one()
    {
        using var empty = TempVault.Empty();
        _ = Journal.Entries;

        await Host.OpenAsync(empty.Root);

        Journal.Entries.Should().BeEmpty();
    }

    [Fact]
    public void ForCreature_finds_the_entries_pointing_to_an_srd_key()
    {
        var glossary = Services.GetRequiredService<GlossaryService>().Index;

        Journal.ForCreature("srd:criaturas/ankheg", glossary).Select(e => e.Title).Should().Equal("Ankheg");
    }

    [Fact]
    public void ForCreature_finds_the_entries_pointing_to_a_vault_note_with_a_wikilink()
    {
        var glossary = Services.GetRequiredService<GlossaryService>().Index;

        Journal.ForCreature("vault:Monstruos/Lobo.md", glossary).Select(e => e.Title).Should().Equal("Lobo");
    }

    [Fact]
    public async Task ForCreature_counts_both_the_srd_entry_and_the_vault_note_that_covers_it()
    {
        await Host.Current!.SaveAsync("Monstruos/Ankheg.md", "---\ntype: monster\nname: Ankheg\nsource: srd:criaturas/ankheg\n---\nMi ankheg.\n", WritePrecondition.MustNotExist);
        await Host.Current.SaveAsync(LyraDiary + "/2026-11-17 Otro.md", EntryText("2026-11-17T10:00:00+01:00", ["x.png"], "[[Ankheg]]"), WritePrecondition.MustNotExist);
        var glossary = Services.GetRequiredService<GlossaryService>().Index;

        var viaSrd = Journal.ForCreature("srd:criaturas/ankheg", glossary).Select(e => e.Title);
        var viaNote = Journal.ForCreature("vault:Monstruos/Ankheg.md", glossary).Select(e => e.Title);

        (string.Join(",", viaSrd), string.Join(",", viaNote)).Should().Be(("Otro,Ankheg", "Otro,Ankheg"));
    }

    [Fact]
    public void ForCreature_of_an_unrelated_key_is_empty()
    {
        var glossary = Services.GetRequiredService<GlossaryService>().Index;

        Journal.ForCreature("srd:criaturas/aboleth", glossary).Should().BeEmpty();
    }

    [Fact]
    public void CreatureNameOf_is_the_glossary_name_of_the_linked_creature()
    {
        var glossary = Services.GetRequiredService<GlossaryService>().Index;

        (Journal.CreatureNameOf(Journal.Get(Ankheg)!, glossary), Journal.CreatureNameOf(Journal.Get(Lobo)!, glossary))
            .Should().Be((glossary.Get("srd:criaturas/ankheg")!.Name, "Lobo"));
    }

    [Fact]
    public void CreatureNameOf_is_null_without_a_creature_or_when_it_does_not_resolve()
    {
        var glossary = Services.GetRequiredService<GlossaryService>().Index;

        (Journal.CreatureNameOf(Journal.Get(Faro)!, glossary), Journal.CreatureNameOf(EntryWith("[[No existe]]"), glossary))
            .Should().Be((null, null));
    }

    [Theory]
    [InlineData("srd:criaturas/ankheg", "srd:criaturas/ankheg")]
    [InlineData("[[Lobo]]", "vault:Monstruos/Lobo.md")]
    [InlineData("[[Lobo|el lobo]]", "vault:Monstruos/Lobo.md")]
    [InlineData("[[Monstruos/Lobo#Notas]]", "vault:Monstruos/Lobo.md")]
    [InlineData("[[No existe]]", null)]
    [InlineData(null, null)]
    public void CreatureKeyOf_maps_what_the_note_says_to_a_glossary_key(string? creature, string? expected) =>
        Journal.CreatureKeyOf(EntryWith(creature)).Should().Be(expected);

    [Fact]
    public void LastWriteOf_is_the_date_of_the_note_in_the_vault() =>
        Journal.LastWriteOf(Ankheg).Should().Be(File.GetLastWriteTimeUtc(Vault.PathOf(Ankheg)));

    [Fact]
    public void ImageUrl_is_a_vault_url_for_an_image_that_can_be_shown() =>
        Journal.ImageUrl(Journal.Get(Ankheg)!.Images[0]).Should().StartWith("/_vault/Personajes/Lyra/Diario/2026-11-16%20Ankheg-1.jpg?v=");

    [Fact]
    public async Task ImageUrl_is_null_for_a_missing_image()
    {
        await Journal.RefreshImagesAsync();

        Journal.ImageUrl(Journal.Get(Faro)!.Images[0]).Should().BeNull();
    }

    [Fact]
    public void ImageUrl_is_null_for_a_format_that_cannot_be_shown() =>
        Journal.ImageUrl(new JournalImage("a.heic", LyraDiary + "/a.heic", true, false)).Should().BeNull();
}
