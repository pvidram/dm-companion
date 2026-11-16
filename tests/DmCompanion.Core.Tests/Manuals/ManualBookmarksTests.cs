using DmCompanion.Core.Manuals;

namespace DmCompanion.Core.Tests.Manuals;

public class ManualBookmarksTests
{
    private static ManualBookmarkFile File(params ManualBookmark[] items) => new(1, items);

    [Fact]
    public void Missing_file_is_not_corrupt() =>
        ManualBookmarks.Parse(null).Should().Be(new BookmarksRead(null, false));

    [Fact]
    public void Invalid_json_is_corrupt()
    {
        var read = ManualBookmarks.Parse("{ no");
        (read.File, read.Corrupt).Should().Be((null, true));
    }

    [Fact]
    public void Future_version_is_corrupt() =>
        ManualBookmarks.Parse("{\"version\":2,\"bookmarks\":[]}").Corrupt.Should().BeTrue();

    [Fact]
    public void Roundtrip()
    {
        var file = File(new ManualBookmark("a", "Inicio", 3), new ManualBookmark("b", "Magia", 10));
        ManualBookmarks.Parse(ManualBookmarks.Serialize(file)).File.Should().BeEquivalentTo(file);
    }

    [Fact]
    public void Serialize_is_camel_case_with_trailing_newline()
    {
        var json = ManualBookmarks.Serialize(File(new ManualBookmark("a", "Inicio", 3)));
        (json.EndsWith('\n'), json.Contains("\"bookmarks\"")).Should().Be((true, true));
    }

    [Fact]
    public void Merge_keeps_external_additions_and_mine()
    {
        var a = new ManualBookmark("a", "A", 1);
        var mine = new ManualBookmark("m", "Mío", 2);
        var theirs = new ManualBookmark("t", "Ajeno", 3);
        var merged = ManualBookmarks.Merge(File(a), File(a, mine), File(a, theirs));
        merged.Bookmarks.Select(b => b.Id).Should().Equal("a", "t", "m");
    }

    [Fact]
    public void Merge_applies_my_deletions()
    {
        var a = new ManualBookmark("a", "A", 1);
        var b = new ManualBookmark("b", "B", 2);
        var merged = ManualBookmarks.Merge(File(a, b), File(a), File(a, b));
        merged.Bookmarks.Select(x => x.Id).Should().Equal("a");
    }

    [Fact]
    public void Null_bookmark_is_corrupt() =>
        ManualBookmarks.Parse("{\"version\":1,\"bookmarks\":[null]}").Corrupt.Should().BeTrue();

    [Theory]
    [InlineData("{\"version\":1,\"bookmarks\":[{\"id\":\"\",\"name\":\"A\",\"page\":1}]}")]
    [InlineData("{\"version\":1,\"bookmarks\":[{\"name\":\"A\",\"page\":1}]}")]
    [InlineData("{\"version\":1,\"bookmarks\":[{\"id\":\"a\",\"name\":\"\",\"page\":1}]}")]
    public void Empty_id_is_corrupt(string json) => ManualBookmarks.Parse(json).Corrupt.Should().BeTrue();

    [Fact]
    public void Page_below_one_is_corrupt() =>
        ManualBookmarks.Parse("{\"version\":1,\"bookmarks\":[{\"id\":\"a\",\"name\":\"A\",\"page\":0}]}")
            .Corrupt.Should().BeTrue();

    [Fact]
    public void Missing_bookmarks_is_corrupt() =>
        ManualBookmarks.Parse("{\"version\":1}").Corrupt.Should().BeTrue();
}
