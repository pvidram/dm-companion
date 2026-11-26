using AngleSharp.Dom;
using Bunit;
using DmCompanion.Screens.Components;
using DmCompanion.Screens.Platform;
using DmCompanion.Screens.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace DmCompanion.Screens.Tests;

public sealed class JournalFormSavingTests : JournalUiTestBase
{
    private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private sealed class BlockingPicker(TaskCompletionSource release) : IImagePicker
    {
        public Task<PickedImage?> PickAsync() => Task.FromResult<PickedImage?>(null);

        public Task<IReadOnlyList<PickedImage>> PickManyAsync() => Task.FromResult<IReadOnlyList<PickedImage>>(
            [new PickedImage("uno.png", async () =>
            {
                await release.Task;
                return new MemoryStream([1, 2, 3]);
            })]);
    }

    public JournalFormSavingTests()
    {
        Services.AddSingleton<JournalStore>();
        Services.AddSingleton<IImagePicker>(new BlockingPicker(_release));
    }

    private static IElement ButtonLabeled(IRenderedComponent<JournalFormDialog> cut, string label) =>
        cut.FindAll("button").First(b => b.TextContent.Trim() == label);

    private async Task<IRenderedComponent<JournalFormDialog>> StartSavingAsync()
    {
        // Como en JournalFormDialogTests: el listado de imágenes repinta; se espera antes para que los manejadores no caduquen.
        await Journal.RefreshImagesAsync();
        var cut = Render<JournalFormDialog>();
        await cut.InvokeAsync(() => Shell.OpenJournalForm(new JournalFormRequest(JournalFormMode.New, Lyra)));
        ButtonLabeled(cut, "Añadir imagen").Click();
        cut.WaitForAssertion(() => ButtonLabeled(cut, "Guardar").HasAttribute("disabled").Should().BeFalse(), Wait);
        ButtonLabeled(cut, "Guardar").Click();
        cut.WaitForAssertion(() => Shell.IsJournalFormSaving.Should().BeTrue(), Wait);
        return cut;
    }

    [Fact]
    public async Task While_saving_Descartar_is_disabled()
    {
        var cut = await StartSavingAsync();

        var disabled = ButtonLabeled(cut, "Descartar").HasAttribute("disabled");
        _release.SetResult();

        disabled.Should().BeTrue();
    }

    [Fact]
    public async Task While_saving_Escape_and_back_do_not_close_the_form()
    {
        var cut = await StartSavingAsync();

        await cut.InvokeAsync(() => cut.Find("[role=dialog]").KeyDown(new KeyboardEventArgs { Key = "Escape" }));
        await cut.InvokeAsync(Shell.RequestCloseJournalForm);
        var open = Shell.JournalForm is not null;
        _release.SetResult();

        open.Should().BeTrue();
    }

    [Fact]
    public async Task When_the_save_ends_the_form_closes_normally()
    {
        var cut = await StartSavingAsync();

        _release.SetResult();

        cut.WaitForAssertion(() => Shell.JournalForm.Should().BeNull(), Wait);
    }
}
