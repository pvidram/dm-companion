using DmCompanion.Screens.Platform;
using DmCompanion.Screens.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace DmCompanion.Screens.Tests;

public sealed class IncomingShareTests : IDisposable
{
    private sealed class FakeInbox(string folder) : IShareInbox
    {
        public string TempFolder { get; } = folder;
    }

    private readonly string _temp = Path.Combine(Path.GetTempPath(), "dmc-tests", Guid.NewGuid().ToString("N"), "share");
    private readonly IncomingShare _share;

    public IncomingShareTests()
    {
        Directory.CreateDirectory(_temp);
        _share = new IncomingShare(NullLogger<IncomingShare>.Instance, new FakeInbox(_temp));
    }

    public void Dispose() => Directory.Delete(Directory.GetParent(_temp)!.FullName, recursive: true);

    private SharedFile NewFile(string name = "1.jpg")
    {
        var batch = Path.Combine(_temp, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(batch);
        var path = Path.Combine(batch, name);
        File.WriteAllBytes(path, [1, 2, 3]);
        return new SharedFile(name, path);
    }

    [Fact]
    public void Receive_raises_Received_once()
    {
        var raised = 0;
        _share.Received += (_, _) => raised++;

        _share.Receive([NewFile()], 0);

        raised.Should().Be(1);
    }

    [Fact]
    public void Receive_with_nothing_does_not_raise()
    {
        var raised = 0;
        _share.Received += (_, _) => raised++;

        _share.Receive([], 0);

        raised.Should().Be(0);
    }

    [Fact]
    public void Take_returns_everything_received()
    {
        _share.Receive([NewFile("1.jpg")], 1);
        _share.Receive([NewFile("2.png")], 2);

        var batch = _share.Take();

        (string.Join(",", batch.Files.Select(f => f.FileName)), batch.Unreadable).Should().Be(("1.jpg,2.png", 3));
    }

    [Fact]
    public void Take_empties_the_pending_list()
    {
        _share.Receive([NewFile()], 1);

        _share.Take();

        _share.HasPending.Should().BeFalse();
    }

    [Fact]
    public void Receive_that_only_has_unreadable_files_is_pending()
    {
        _share.Receive([], 2);

        _share.HasPending.Should().BeTrue();
    }

    [Fact]
    public async Task Open_reads_the_temp_file()
    {
        var file = NewFile();

        await using var stream = _share.Open(file);

        stream.Length.Should().Be(3);
    }

    [Fact]
    public void Discard_deletes_the_temp_file_and_its_empty_batch_folder()
    {
        var file = NewFile();

        _share.Discard([file]);

        (File.Exists(file.TempPath), Directory.Exists(Path.GetDirectoryName(file.TempPath))).Should().Be((false, false));
    }

    [Fact]
    public void Discard_never_deletes_outside_the_temp_folder()
    {
        var outside = Path.Combine(Directory.GetParent(_temp)!.FullName, "vault-note.md");
        File.WriteAllText(outside, "mía");

        _share.Discard([new SharedFile("vault-note.md", outside)]);

        File.Exists(outside).Should().BeTrue();
    }

    [Fact]
    public void CleanTemp_empties_the_temp_folder()
    {
        NewFile();
        NewFile();

        _share.CleanTemp();

        Directory.EnumerateFileSystemEntries(_temp).Should().BeEmpty();
    }

    [Theory]
    [InlineData("image/jpeg", ".jpg")]
    [InlineData("image/png", ".png")]
    [InlineData("image/webp", ".webp")]
    [InlineData("image/gif", ".gif")]
    [InlineData("image/heic", ".heic")]
    [InlineData("image/heif", ".heic")]
    [InlineData("IMAGE/JPEG", ".jpg")]
    [InlineData("image/bmp", null)]
    [InlineData("application/pdf", null)]
    [InlineData(null, null)]
    public void Mime_type_maps_to_a_supported_extension(string? mime, string? expected) =>
        IncomingShare.ExtensionForMime(mime).Should().Be(expected);
}
