using Bunit;
using DmCompanion.Core.Maps;
using DmCompanion.Core.Vault.Storage;
using DmCompanion.Infrastructure;
using DmCompanion.Screens.Components;
using DmCompanion.Screens.Platform;
using DmCompanion.Screens.Services;
using DmCompanion.UI;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DmCompanion.Screens.Tests;

public sealed class MapNameDialogPickerTests : MapsTestContext
{
    protected override void Configure() =>
        Services.AddSingleton<IImagePicker>(new StubPicker(new PickedImage("Pozo.jpg", () => Task.FromResult<Stream>(new MemoryStream([9])))));

    [Fact]
    public void Import_dialog_with_a_native_picker_takes_the_image_from_it()
    {
        var cut = RenderOpenMaps();
        cut.FindAll("section.ds-parchment .ds-parchment__actions button")[1].Click();
        cut.WaitForAssertion(() => OpenDialogText(cut).Should().Contain("Elegir imagen"), Wait);

        OpenDialog(cut).QuerySelectorAll("button").First(b => b.TextContent.Trim() == "Elegir imagen").Click();

        cut.WaitForAssertion(() => OpenDialog(cut).QuerySelector("input[type=text]")!.GetAttribute("value").Should().Be("Pozo"), Wait);
    }

    private sealed class StubPicker(PickedImage? image) : IImagePicker
    {
        public Task<PickedImage?> PickAsync() => Task.FromResult(image);
    }
}