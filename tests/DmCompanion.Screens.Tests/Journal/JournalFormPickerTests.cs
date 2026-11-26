using Bunit;
using DmCompanion.Screens.Components;
using DmCompanion.Screens.Platform;
using DmCompanion.Screens.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DmCompanion.Screens.Tests;

public sealed class JournalFormPickerTests : JournalUiTestBase
{
    private sealed class StubPicker(params string[] names) : IImagePicker
    {
        public Task<PickedImage?> PickAsync() => Task.FromResult<PickedImage?>(null);

        public Task<IReadOnlyList<PickedImage>> PickManyAsync() =>
            Task.FromResult<IReadOnlyList<PickedImage>>([.. names.Select(name => new PickedImage(name, () => Task.FromResult<Stream>(new MemoryStream([1, 2, 3]))))]);
    }

    public JournalFormPickerTests()
    {
        Services.AddSingleton<JournalStore>();
        Services.AddSingleton<IImagePicker>(new StubPicker("uno.png", "dos.webp", "nota.pdf"));
    }

    [Fact]
    public async Task With_a_native_picker_the_button_adds_the_images_it_returns_and_discards_unsupported_ones()
    {
        var cut = Render<JournalFormDialog>();
        await cut.InvokeAsync(() => Shell.OpenJournalForm(new JournalFormRequest(JournalFormMode.New, Lyra)));

        cut.FindAll("button").First(b => b.TextContent.Trim() == "Añadir imagen").Click();

        cut.WaitForAssertion(() => (cut.FindAll(".journal-form__image-name").Count, cut.FindAll("input[type=file]").Count, cut.Markup.Contains("No se pudo leer 1 imagen(es)", StringComparison.Ordinal))
            .Should().Be((2, 0, true)), Wait);
    }
}
