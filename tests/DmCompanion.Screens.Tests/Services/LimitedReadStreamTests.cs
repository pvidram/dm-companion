using DmCompanion.Screens.Services;

namespace DmCompanion.Screens.Tests.Services;

public sealed class LimitedReadStreamTests
{
    private static byte[] Data() => Enumerable.Range(0, 100).Select(i => (byte)i).ToArray();

    private sealed class NoSeekStream(byte[] data) : MemoryStream(data)
    {
        public override bool CanSeek => false;
    }

    private static async Task<byte[]> ReadAllAsync(Stream stream)
    {
        using var copy = new MemoryStream();
        await stream.CopyToAsync(copy);
        return copy.ToArray();
    }

    [Fact]
    public async Task Reads_only_the_slice()
    {
        await using var stream = new LimitedReadStream(new MemoryStream(Data()), 10, 5);

        (await ReadAllAsync(stream)).Should().Equal(10, 11, 12, 13, 14);
    }

    [Fact]
    public async Task Works_without_seek()
    {
        await using var stream = new LimitedReadStream(new NoSeekStream(Data()), 10, 5);

        (await ReadAllAsync(stream)).Should().Equal(10, 11, 12, 13, 14);
    }

    [Fact]
    public async Task Does_not_read_beyond_the_length()
    {
        var inner = new MemoryStream(Data());
        await using var stream = new LimitedReadStream(inner, 10, 5);

        await ReadAllAsync(stream);

        inner.Position.Should().Be(15);
    }
}
