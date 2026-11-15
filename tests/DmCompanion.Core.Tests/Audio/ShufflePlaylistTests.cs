using DmCompanion.Core.Audio;

namespace DmCompanion.Core.Tests.Audio;

public class ShufflePlaylistTests
{
    private static readonly string[] Tracks = ["a", "b", "c", "d"];

    [Fact]
    public void Plays_every_track_once_per_round()
    {
        var playlist = new ShufflePlaylist(Tracks, new Random(1));

        var round = Enumerable.Range(0, Tracks.Length).Select(_ => playlist.Next()).ToList();

        round.Should().BeEquivalentTo(Tracks);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void New_round_does_not_start_with_the_last_track(int seed)
    {
        var playlist = new ShufflePlaylist(["a", "b"], new Random(seed));
        var plays = Enumerable.Range(0, 40).Select(_ => playlist.Next()).ToList();

        var repeats = plays.Zip(plays.Skip(1)).Count(pair => pair.First == pair.Second);

        repeats.Should().Be(0);
    }

    [Fact]
    public void Single_track_repeats()
    {
        var playlist = new ShufflePlaylist(["solo"], new Random(1));

        Enumerable.Range(0, 5).Select(_ => playlist.Next()).Should().AllBe("solo");
    }

    [Fact]
    public void Empty_returns_null() =>
        new ShufflePlaylist([], new Random(1)).Next().Should().BeNull();
}
