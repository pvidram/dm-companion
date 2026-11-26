using AngleSharp.Dom;
using Bunit;
using DmCompanion.Screens.Components;
using DmCompanion.Screens.Services;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace DmCompanion.Screens.Tests;

public sealed class JournalFormReadingTests : JournalUiTestBase
{
    private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private sealed class SlowFile(string name, TaskCompletionSource release) : IBrowserFile
    {
        public string Name => name;
        public DateTimeOffset LastModified => DateTimeOffset.UtcNow;
        public long Size => 3;
        public string ContentType => "image/png";

        public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default) => new SlowStream(release);
    }

    private sealed class SlowStream(TaskCompletionSource release) : MemoryStream([1, 2, 3])
    {
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await release.Task;
            return await base.ReadAsync(buffer, cancellationToken);
        }
    }

    public JournalFormReadingTests() => Services.AddSingleton<JournalStore>();

    private static IElement ButtonLabeled(IRenderedComponent<JournalFormDialog> cut, string label) =>
        cut.FindAll("button").First(b => b.TextContent.Trim() == label);

    private async Task<IRenderedComponent<JournalFormDialog>> StartReadingAsync()
    {
        await Journal.RefreshImagesAsync();
        var cut = Render<JournalFormDialog>();
        await cut.InvokeAsync(() => Shell.OpenJournalForm(new JournalFormRequest(JournalFormMode.New, Lyra)));
        await cut.InvokeAsync(() => cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromBinary([1, 2, 3], "uno.png")));
        cut.WaitForAssertion(() => ButtonLabeled(cut, "Guardar").HasAttribute("disabled").Should().BeFalse(), Wait);
        var images = cut.FindComponent<JournalFormImages>().Instance;
        _ = cut.InvokeAsync(() => images.OnFilesChosen.InvokeAsync(new InputFileChangeEventArgs([new SlowFile("dos.png", _release)])));
        cut.WaitForAssertion(() => ButtonLabeled(cut, "Guardar").HasAttribute("disabled").Should().BeTrue(), Wait);
        return cut;
    }

    [Fact]
    public async Task While_reading_the_chosen_files_Guardar_and_Descartar_are_disabled()
    {
        var cut = await StartReadingAsync();

        var disabled = (ButtonLabeled(cut, "Guardar").HasAttribute("disabled"), ButtonLabeled(cut, "Descartar").HasAttribute("disabled"));
        _release.SetResult();

        disabled.Should().Be((true, true));
    }

    [Fact]
    public async Task While_reading_the_chosen_files_Escape_and_back_do_not_close_the_form()
    {
        var cut = await StartReadingAsync();

        await cut.InvokeAsync(() => cut.Find("[role=dialog]").KeyDown(new KeyboardEventArgs { Key = "Escape" }));
        await cut.InvokeAsync(Shell.RequestCloseJournalForm);
        var open = Shell.JournalForm is not null;
        _release.SetResult();

        open.Should().BeTrue();
    }

    [Fact]
    public async Task When_the_reading_ends_Guardar_is_enabled_again_with_both_images()
    {
        var cut = await StartReadingAsync();

        _release.SetResult();

        cut.WaitForAssertion(() => (ButtonLabeled(cut, "Guardar").HasAttribute("disabled"), cut.FindAll(".journal-form__image").Count).Should().Be((false, 2)), Wait);
    }
}
