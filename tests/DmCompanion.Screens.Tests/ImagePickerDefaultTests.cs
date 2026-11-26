using DmCompanion.Screens.Platform;

namespace DmCompanion.Screens.Tests;

public class ImagePickerDefaultTests
{
    private sealed class SinglePicker(PickedImage? image) : IImagePicker
    {
        public Task<PickedImage?> PickAsync() => Task.FromResult(image);
    }

    [Fact]
    public async Task PickMany_wraps_a_single_pick_by_default()
    {
        IImagePicker picker = new SinglePicker(new PickedImage("a.jpg", () => Task.FromResult<Stream>(new MemoryStream())));

        var picked = await picker.PickManyAsync();

        picked.Select(p => p.FileName).Should().Equal("a.jpg");
    }

    [Fact]
    public async Task PickMany_is_empty_when_the_single_pick_is_cancelled()
    {
        IImagePicker picker = new SinglePicker(null);

        (await picker.PickManyAsync()).Should().BeEmpty();
    }
}
