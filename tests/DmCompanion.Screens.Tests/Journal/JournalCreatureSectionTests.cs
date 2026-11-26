using Bunit;
using DmCompanion.Screens.Components;
using DmCompanion.Screens.Services;

namespace DmCompanion.Screens.Tests;

public sealed class JournalCreatureSectionTests : JournalUiTestBase
{
    private const string AnkhegKey = "srd:criaturas/ankheg";
    private const string Ankheg = LyraDiary + "/2026-11-16 Ankheg.md";
    private const string Faro = TorvaldDiary + "/2026-11-12 Faro.md";
    private const string Lobo = LyraDiary + "/2026-11-10 Lobo.md";
    private const string Raro = "Otros/Nadie/Diario/2026-11-01 Raro.md";

    protected override Task SeedAsync()
    {
        WriteFile("Monstruos/Ankheg.md", $"---\ntype: monster\nname: Ankheg\nsource: {AnkhegKey}\n---\nMi ankheg.\n");
        WriteFile("Monstruos/Lobo.md", "---\ntype: monster\nname: Lobo\n---\nLobo.\n");
        WriteFile(Ankheg, EntryText("2026-11-16T21:34:00+01:00", ["2026-11-16 Ankheg-1.jpg"], AnkhegKey));
        WriteImage(LyraDiary + "/2026-11-16 Ankheg-1.jpg");
        WriteFile(Faro, EntryText("2026-11-12T19:00:00+01:00", ["falta.png"], "[[Ankheg]]"));
        WriteFile(Lobo, EntryText("2026-11-10T20:00:00+01:00", ["2026-11-10 Lobo-1.png"], "[[Lobo]]"));
        WriteImage(LyraDiary + "/2026-11-10 Lobo-1.png");
        WriteFile(Raro, EntryText("2026-11-01T10:00:00+01:00", ["2026-11-01 Raro-1.png"], AnkhegKey));
        WriteImage("Otros/Nadie/Diario/2026-11-01 Raro-1.png");
        return Task.CompletedTask;
    }

    private IRenderedComponent<JournalCreatureSection> RenderSection(string key) =>
        Render<JournalCreatureSection>(p => p.Add(c => c.Key, key));

    private static string[] Labels(IRenderedComponent<JournalCreatureSection> cut) =>
        [.. cut.FindAll(".journal-thumb__label").Select(e => e.TextContent)];

    [Fact]
    public void It_shows_the_heading_and_a_thumbnail_per_entry_newest_first_with_the_pc_and_the_title()
    {
        var cut = RenderSection(AnkhegKey);

        (cut.Find("h4").TextContent, string.Join("|", Labels(cut))).Should().Be(("Lo que saben los jugadores", "Lyra · Ankheg|Torvald · Faro|Nadie · Raro"));
    }

    [Fact]
    public void A_vault_note_that_covers_the_srd_entry_shows_the_same_entries()
    {
        var cut = RenderSection("vault:Monstruos/Ankheg.md");

        string.Join("|", Labels(cut)).Should().Be("Lyra · Ankheg|Torvald · Faro|Nadie · Raro");
    }

    [Fact]
    public void A_creature_only_has_its_own_entries()
    {
        var cut = RenderSection("vault:Monstruos/Lobo.md");

        string.Join("|", Labels(cut)).Should().Be("Lyra · Lobo");
    }

    [Fact]
    public void A_creature_nobody_has_seen_renders_nothing() =>
        RenderSection("srd:criaturas/aboleth").Markup.Trim().Should().BeEmpty();

    [Fact]
    public async Task Touching_a_thumbnail_opens_the_entry_in_the_diary_tab_of_its_pc()
    {
        var cut = RenderSection(AnkhegKey);

        await cut.InvokeAsync(() => cut.FindAll(".journal-thumb")[1].Click());

        (Shell.ActivePanel, Shell.SelectedPcPath, Shell.PlayerTab, Shell.SelectedJournalPath).Should().Be((OverlayPanel.Players, Torvald, PlayerTab.Journal, Faro));
    }

    [Fact]
    public void An_entry_whose_folder_is_no_pc_is_shown_but_cannot_be_opened()
    {
        var cut = RenderSection(AnkhegKey);

        var thumbs = cut.FindAll(".journal-thumb").Select(t => t.HasAttribute("disabled")).ToArray();

        thumbs.Should().Equal(false, false, true);
    }

    [Fact]
    public void The_thumbnails_show_the_images_of_the_entries()
    {
        var cut = RenderSection("vault:Monstruos/Lobo.md");

        cut.Find("img").GetAttribute("src")!.Should().Contain("Lobo-1.png");
    }

    [Fact]
    public async Task A_new_entry_appears_without_reopening_the_sheet()
    {
        var cut = RenderSection("vault:Monstruos/Lobo.md");

        await Host.Current!.SaveAsync(TorvaldDiary + "/2026-11-18 Otro lobo.md", EntryText("2026-11-18T10:00:00+01:00", ["x.png"], "[[Lobo]]"), DmCompanion.Core.Vault.Storage.WritePrecondition.MustNotExist);

        cut.WaitForAssertion(() => string.Join("|", Labels(cut)).Should().Be("Torvald · Otro lobo|Lyra · Lobo"), Wait);
    }

    [Fact]
    public async Task With_unsaved_note_text_touching_a_thumbnail_asks_before_navigating()
    {
        var cut = RenderSection(AnkhegKey);
        Shell.SetEditorDirty(true);

        await cut.InvokeAsync(() => cut.FindAll(".journal-thumb")[0].Click());

        (Shell.ActivePanel, Shell.ActiveEditorDialog).Should().Be((OverlayPanel.None, EditorDialog.ConfirmClose));
    }
}
