using AngleSharp.Dom;
using Bunit;
using DmCompanion.Screens.Components;
using DmCompanion.Screens.Platform;
using DmCompanion.Screens.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DmCompanion.Screens.Tests;

public sealed class JournalFormShareTests : JournalUiTestBase
{
    private sealed class FakeInbox(string folder) : IShareInbox
    {
        public string TempFolder { get; } = folder;
    }

    private sealed class Gate
    {
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public volatile bool Opened;

        public async Task<Stream> WaitAsync()
        {
            Opened = true;
            await _release.Task;
            return new MemoryStream([1, 2, 3]);
        }

        public void Release() => _release.TrySetResult();
    }

    private sealed class GatedPicker(Gate gate) : IImagePicker
    {
        public Task<PickedImage?> PickAsync() => Task.FromResult<PickedImage?>(null);

        public Task<IReadOnlyList<PickedImage>> PickManyAsync() => Task.FromResult<IReadOnlyList<PickedImage>>([new PickedImage("a.png", gate.WaitAsync)]);
    }

    private readonly Gate _gate = new();

    private string Inbox => Vault.PathOf(".share-inbox");

    public JournalFormShareTests()
    {
        Services.AddSingleton<JournalStore>();
        Services.AddSingleton<IImagePicker>(new GatedPicker(_gate));
        Services.AddSingleton(_ => new IncomingShare(NullLogger<IncomingShare>.Instance, new FakeInbox(Inbox)));
    }

    private IncomingShare Share => Services.GetRequiredService<IncomingShare>();

    private SharedFile Temp(string name, byte marker)
    {
        var folder = Path.Combine(Inbox, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, name);
        File.WriteAllBytes(path, [marker, 2, 3]);
        return new SharedFile(name, path);
    }

    private IEnumerable<string> TempFiles() =>
        Directory.Exists(Inbox) ? Directory.EnumerateFiles(Inbox, "*", SearchOption.AllDirectories) : [];

    private IRenderedComponent<JournalFormDialog> RenderForm() => Render<JournalFormDialog>();

    private static IElement ButtonLabeled(IRenderedComponent<JournalFormDialog> cut, string label) =>
        cut.FindAll("button").First(b => b.TextContent.Trim() == label);

    private static IReadOnlyList<string> ImageNames(IRenderedComponent<JournalFormDialog> cut) =>
        [.. cut.FindAll(".journal-form__image-name").Select(e => e.TextContent.Trim())];

    [Fact]
    public void A_shared_batch_opens_the_form_with_its_images_in_order()
    {
        var cut = RenderForm();

        Share.Receive([Temp("imagen 1.png", 1), Temp("imagen 2.jpg", 2)], 0);

        cut.WaitForAssertion(() => (Shell.JournalForm?.Mode, string.Join(",", ImageNames(cut))).Should().Be(((JournalFormMode?)JournalFormMode.Shared, "imagen 1.png,imagen 2.jpg")), Wait);
    }

    [Fact]
    public void The_batch_is_taken_from_the_inbox()
    {
        var cut = RenderForm();

        Share.Receive([Temp("imagen 1.png", 1)], 0);

        cut.WaitForAssertion(() => Share.HasPending.Should().BeFalse(), Wait);
    }

    [Fact]
    public void A_share_that_arrived_before_the_shell_opens_the_form_when_it_appears()
    {
        Share.Receive([Temp("imagen 1.png", 1)], 0);

        var cut = RenderForm();

        cut.WaitForAssertion(() => (Shell.JournalForm?.Mode, ImageNames(cut).Count).Should().Be(((JournalFormMode?)JournalFormMode.Shared, 1)), Wait);
    }

    [Fact]
    public void Shared_images_can_be_reordered_and_one_removed_before_saving()
    {
        var cut = RenderForm();
        Share.Receive([Temp("a.png", 1), Temp("b.png", 2), Temp("c.png", 3)], 0);
        cut.WaitForAssertion(() => ImageNames(cut).Count.Should().Be(3), Wait);

        cut.FindAll("li.journal-form__image")[0].QuerySelectorAll("button").First(b => b.TextContent.Trim() == "Bajar").Click();
        cut.FindAll("li.journal-form__image")[2].QuerySelectorAll("button").First(b => b.TextContent.Trim() == "Quitar").Click();
        ButtonLabeled(cut, "Guardar").Click();

        cut.WaitForAssertion(() => Shell.JournalForm.Should().BeNull(), Wait);
        var saved = Journal.ForPc(Lyra).Single();
        (saved.Images.Count, File.ReadAllBytes(Vault.PathOf(saved.Images[0].Path))[0], File.ReadAllBytes(Vault.PathOf(saved.Images[1].Path))[0])
            .Should().Be((2, (byte)2, (byte)1));
    }

    [Fact]
    public void Saving_empties_the_temporary_folder()
    {
        var cut = RenderForm();
        Share.Receive([Temp("a.png", 1), Temp("b.png", 2)], 0);
        cut.WaitForAssertion(() => ImageNames(cut).Count.Should().Be(2), Wait);

        ButtonLabeled(cut, "Guardar").Click();

        cut.WaitForAssertion(() => Shell.JournalForm.Should().BeNull(), Wait);
        TempFiles().Should().BeEmpty();
    }

    [Fact]
    public void Discarding_empties_the_temporary_folder_and_saves_nothing()
    {
        var cut = RenderForm();
        Share.Receive([Temp("a.png", 1)], 0);
        cut.WaitForAssertion(() => ImageNames(cut).Count.Should().Be(1), Wait);

        ButtonLabeled(cut, "Descartar").Click();

        cut.WaitForAssertion(() => (Shell.JournalForm, TempFiles().Count(), Directory.Exists(Vault.PathOf(LyraDiary))).Should().Be((null, 0, false)), Wait);
    }

    [Fact]
    public void A_removed_shared_image_is_also_deleted_from_the_temporary_folder()
    {
        var cut = RenderForm();
        var kept = Temp("a.png", 1);
        var dropped = Temp("b.png", 2);
        Share.Receive([kept, dropped], 0);
        cut.WaitForAssertion(() => ImageNames(cut).Count.Should().Be(2), Wait);
        cut.FindAll("li.journal-form__image")[1].QuerySelectorAll("button").First(b => b.TextContent.Trim() == "Quitar").Click();

        ButtonLabeled(cut, "Descartar").Click();

        cut.WaitForAssertion(() => TempFiles().Should().BeEmpty(), Wait);
    }

    [Fact]
    public void The_unreadable_count_of_the_share_is_shown_and_kept_after_saving()
    {
        var cut = RenderForm();
        Share.Receive([Temp("a.png", 1)], 2);
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("No se pudo leer 2 imagen(es)"), Wait);

        ButtonLabeled(cut, "Guardar").Click();

        cut.WaitForAssertion(() => Shell.JournalNotice.Should().Be("No se pudo leer 2 imagen(es)"), Wait);
    }

    [Fact]
    public void A_share_with_only_unreadable_files_opens_the_form_with_the_notice_and_no_images()
    {
        var cut = RenderForm();

        Share.Receive([], 3);

        cut.WaitForAssertion(() => (cut.Markup.Contains("No se pudo leer 3 imagen(es)", StringComparison.Ordinal), ImageNames(cut).Count, ButtonLabeled(cut, "Guardar").HasAttribute("disabled"))
            .Should().Be((true, 0, true)), Wait);
    }

    [Fact]
    public async Task A_share_while_a_new_form_is_open_adds_its_images_to_the_same_form()
    {
        var cut = RenderForm();
        await cut.InvokeAsync(() => Shell.OpenJournalForm(new JournalFormRequest(JournalFormMode.New, Torvald)));
        var request = Shell.JournalForm;

        Share.Receive([Temp("a.png", 1)], 0);

        cut.WaitForAssertion(() => (ReferenceEquals(Shell.JournalForm, request), ImageNames(cut).Count).Should().Be((true, 1)), Wait);
    }

    [Fact]
    public async Task A_share_while_editing_waits_until_the_form_is_closed()
    {
        var created = await Services.GetRequiredService<JournalStore>().CreateAsync(new JournalCreate(Lyra, "Ankheg", null, null,
            [new JournalImageSource("a.png", () => Task.FromResult<Stream>(new MemoryStream([1, 2, 3])))]));
        var cut = RenderForm();
        await cut.InvokeAsync(() => Shell.OpenJournalForm(new JournalFormRequest(JournalFormMode.Edit, null, created.EntryPath)));

        Share.Receive([Temp("n.png", 1)], 0);
        await Task.Delay(200);
        var whileEditing = (Share.HasPending, Shell.JournalForm?.Mode);
        await cut.InvokeAsync(Shell.CloseJournalForm);

        cut.WaitForAssertion(() => (whileEditing, Shell.JournalForm?.Mode, ImageNames(cut).Count).Should().Be(((true, (JournalFormMode?)JournalFormMode.Edit), (JournalFormMode?)JournalFormMode.Shared, 1)), Wait);
    }

    [Fact]
    public async Task Escape_on_a_shared_form_asks_and_discarding_empties_the_temporary_folder()
    {
        var cut = RenderForm();
        Share.Receive([Temp("a.png", 1)], 0);
        cut.WaitForAssertion(() => ImageNames(cut).Count.Should().Be(1), Wait);

        await cut.InvokeAsync(() => cut.Find("[role=dialog]").KeyDown(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Escape" }));
        var keptWhileAsking = (Shell.JournalForm is not null, TempFiles().Count());
        cut.FindAll(".ds-dialog__footer button").First(b => b.TextContent.Trim() == "Descartar cambios").Click();

        cut.WaitForAssertion(() => (keptWhileAsking, Shell.JournalForm, TempFiles().Count()).Should().Be(((true, 1), (JournalFormRequest?)null, 0)), Wait);
    }

    [Fact]
    public async Task A_share_that_arrives_while_saving_is_not_dragged_into_the_draft_nor_deleted()
    {
        var cut = RenderForm();
        await cut.InvokeAsync(() => Shell.OpenJournalForm(new JournalFormRequest(JournalFormMode.New, Lyra)));
        cut.FindAll("button").First(b => b.TextContent.Trim() == "Añadir imagen").Click();
        cut.WaitForAssertion(() => ImageNames(cut).Count.Should().Be(1), Wait);
        var late = Temp("tarde.png", 9);

        ButtonLabeled(cut, "Guardar").Click();
        await WaitUntilAsync(() => _gate.Opened);
        Share.Receive([late], 0);
        await Task.Delay(100);
        var pendingWhileSaving = Share.HasPending;
        _gate.Release();

        cut.WaitForAssertion(() => (pendingWhileSaving, File.Exists(late.TempPath), Shell.JournalForm?.Mode, ImageNames(cut).SingleOrDefault())
            .Should().Be((true, true, (JournalFormMode?)JournalFormMode.Shared, "tarde.png")), Wait);
    }
}
