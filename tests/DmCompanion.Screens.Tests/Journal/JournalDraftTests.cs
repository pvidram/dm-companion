using DmCompanion.Core.Journal;
using DmCompanion.Screens.Platform;
using DmCompanion.Screens.Services;

namespace DmCompanion.Screens.Tests;

public class JournalDraftTests
{
    private static Func<Task<Stream>> Open => () => Task.FromResult<Stream>(new MemoryStream([1]));

    private static JournalImage Saved(string name) => new(name, "Personajes/Lyra/Diario/" + name, true, true);

    private static string Names(JournalDraft draft) => string.Join(",", draft.Images.Select(i => i.Name));

    private static JournalDraft Three()
    {
        var draft = new JournalDraft();
        draft.AddPicked("a.png", Open);
        draft.AddPicked("b.png", Open);
        draft.AddPicked("c.png", Open);
        return draft;
    }

    private static string IdOf(JournalDraft draft, string name) => draft.Images.First(i => i.Name == name).Id;

    [Fact]
    public void Images_keep_the_order_they_were_added() => Names(Three()).Should().Be("a.png,b.png,c.png");

    [Fact]
    public void Every_image_has_its_own_id() => Three().Images.Select(i => i.Id).Distinct().Count().Should().Be(3);

    [Fact]
    public void MoveUp_swaps_with_the_previous_image()
    {
        var draft = Three();

        var moved = draft.MoveUp(IdOf(draft, "c.png"));

        (moved, Names(draft)).Should().Be((true, "a.png,c.png,b.png"));
    }

    [Fact]
    public void MoveUp_on_the_first_image_does_nothing()
    {
        var draft = Three();

        var moved = draft.MoveUp(IdOf(draft, "a.png"));

        (moved, Names(draft)).Should().Be((false, "a.png,b.png,c.png"));
    }

    [Fact]
    public void MoveDown_swaps_with_the_next_image()
    {
        var draft = Three();

        var moved = draft.MoveDown(IdOf(draft, "a.png"));

        (moved, Names(draft)).Should().Be((true, "b.png,a.png,c.png"));
    }

    [Fact]
    public void MoveDown_on_the_last_image_does_nothing()
    {
        var draft = Three();

        var moved = draft.MoveDown(IdOf(draft, "c.png"));

        (moved, Names(draft)).Should().Be((false, "a.png,b.png,c.png"));
    }

    [Fact]
    public void Remove_takes_the_image_out()
    {
        var draft = Three();

        var removed = draft.Remove(IdOf(draft, "b.png"));

        (removed, Names(draft)).Should().Be((true, "a.png,c.png"));
    }

    [Fact]
    public void The_last_image_cannot_be_removed()
    {
        var draft = new JournalDraft();
        draft.AddPicked("a.png", Open);

        var removed = draft.Remove(draft.Images[0].Id);

        (removed, draft.CanRemove, draft.Count).Should().Be((false, false, 1));
    }

    [Fact]
    public void An_unknown_id_changes_nothing()
    {
        var draft = Three();

        (draft.Remove("nope"), draft.MoveUp("nope"), draft.MoveDown("nope"), Names(draft)).Should().Be((false, false, false, "a.png,b.png,c.png"));
    }

    [Fact]
    public void ToItems_keeps_what_was_saved_by_name_and_adds_what_is_new_in_order()
    {
        var draft = new JournalDraft();
        draft.AddExisting(Saved("2026-11-16 Ankheg-1.png"));
        draft.AddPicked("nueva.jpg", Open);
        draft.AddExisting(Saved("2026-11-16 Ankheg-2.png"));

        var items = draft.ToItems(_ => new MemoryStream());

        string.Join(",", items.Select(i => i.ExistingName ?? "+" + i.Added!.FileName)).Should().Be("2026-11-16 Ankheg-1.png,+nueva.jpg,2026-11-16 Ankheg-2.png");
    }

    [Fact]
    public void ToSources_uses_the_original_file_names_and_streams()
    {
        var draft = new JournalDraft();
        draft.AddPicked("a.png", Open);

        var sources = draft.ToSources(_ => new MemoryStream());

        sources.Select(s => s.FileName).Should().Equal("a.png");
    }

    [Fact]
    public async Task ToSources_opens_a_shared_file_through_the_given_function()
    {
        var draft = new JournalDraft();
        draft.AddShared(new SharedFile("imagen 1.jpg", "/cache/share/abc/1.jpg"));
        string? opened = null;

        var source = draft.ToSources(file => { opened = file.TempPath; return new MemoryStream([7]); })[0];
        await source.OpenAsync();

        (source.FileName, opened).Should().Be(("imagen 1.jpg", "/cache/share/abc/1.jpg"));
    }

    [Fact]
    public void All_shared_files_include_the_ones_that_were_removed()
    {
        var draft = new JournalDraft();
        draft.AddShared(new SharedFile("1.jpg", "/cache/1.jpg"));
        draft.AddShared(new SharedFile("2.jpg", "/cache/2.jpg"));
        draft.AddPicked("c.png", Open);
        draft.Remove(draft.Images[0].Id);

        draft.AllSharedFiles.Select(f => f.TempPath).Should().BeEquivalentTo("/cache/1.jpg", "/cache/2.jpg");
    }

    [Fact]
    public void Existing_images_are_not_shared_files()
    {
        var draft = new JournalDraft();
        draft.AddExisting(Saved("a.png"));

        draft.AllSharedFiles.Should().BeEmpty();
    }
}
