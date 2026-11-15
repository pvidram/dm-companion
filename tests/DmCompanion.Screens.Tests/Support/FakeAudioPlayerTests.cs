using DmCompanion.Screens.Platform;

namespace DmCompanion.Screens.Tests.Support;

public class FakeAudioPlayerTests
{
    [Fact]
    public async Task FakeAudioPlayer_records_volume_and_loop()
    {
        var player = new FakeAudioPlayer();

        await player.PlayAsync("bg", "_audio/a.mp3", 0.5, loop: true);
        await player.SetVolumeAsync("bg", 0.2);

        var channel = player.Channels["bg"];
        (channel.Path, channel.Volume, channel.Loop, channel.Paused).Should().Be(("_audio/a.mp3", 0.2, true, false));
    }

    [Fact]
    public async Task Missing_paths_fail_to_play()
    {
        var player = new FakeAudioPlayer();
        player.MissingPaths.Add("_audio/x.mp3");

        var ok = await player.PlayAsync("bg", "_audio/x.mp3", 0.5, loop: false);

        (ok, player.Channels.ContainsKey("bg")).Should().Be((false, false));
    }

    [Fact]
    public async Task Stop_removes_the_channel_and_pause_flags_the_rest()
    {
        var player = new FakeAudioPlayer();
        await player.PlayAsync("a", "a.mp3", 1, false);
        await player.PlayAsync("b", "b.mp3", 1, false);

        await player.StopAsync("a");
        await player.PauseAllAsync();

        (player.Channels.ContainsKey("a"), player.Channels["b"].Paused).Should().Be((false, true));
    }

    [Fact]
    public async Task A_channel_born_during_the_global_pause_stays_paused_until_resume()
    {
        var player = new FakeAudioPlayer();
        await player.PauseAllAsync();

        await player.PlayAsync("a", "a.mp3", 1, false);
        var pausedBeforeResume = player.Channels["a"].Paused;
        await player.ResumeAllAsync();

        (pausedBeforeResume, player.Channels["a"].Paused).Should().Be((true, false));
    }

    [Fact]
    public async Task Events_can_be_raised_by_tests()
    {
        var player = new FakeAudioPlayer();
        var seen = new List<string>();
        player.Ended += (_, id) => seen.Add("ended:" + id);
        player.Failed += (_, id) => seen.Add("failed:" + id);
        player.ExternalAction += (_, action) => seen.Add(action.ToString());
        await player.PlayAsync("fx", "fx.mp3", 1, false);

        player.RaiseEnded("fx");
        player.RaiseFailed("fx");
        player.RaiseExternal(AudioExternalAction.Pause);

        seen.Should().Equal("ended:fx", "failed:fx", "Pause");
    }
}
