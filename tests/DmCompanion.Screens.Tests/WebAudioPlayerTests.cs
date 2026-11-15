using DmCompanion.Core.Vault;
using DmCompanion.Infrastructure;
using DmCompanion.Screens.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;

namespace DmCompanion.Screens.Tests;

public sealed class WebAudioPlayerTests : IAsyncLifetime
{
    private const string Track = "_audio/fondo/lluvia.mp3";

    private readonly TempVault _vault = TempVault.Empty();
    private readonly RecordingJs _js = new();
    private readonly VaultHost _host;
    private readonly WebAudioPlayer _player;

    public WebAudioPlayerTests()
    {
        _host = new VaultHost(new FileSystemVaultServiceFactory(NullLoggerFactory.Instance), new InMemoryAppPreferences(), NullLogger<VaultHost>.Instance);
        _player = new WebAudioPlayer(_js, new VaultFileUrls(_host), _host, NullLogger<WebAudioPlayer>.Instance);
        Directory.CreateDirectory(Path.GetDirectoryName(_vault.PathOf(Track))!);
        File.WriteAllBytes(_vault.PathOf(Track), [1, 2, 3]);
    }

    async Task IAsyncLifetime.InitializeAsync() => await _host.OpenAsync(_vault.Root);

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _player.DisposeAsync();
        await _host.DisposeAsync();
        _vault.Dispose();
    }

    [Fact]
    public async Task Play_invokes_js_with_the_vault_url_and_volume()
    {
        await _player.PlayAsync("bg", Track, 0.5, true);

        var call = _js.Calls.Single(c => c.Identifier == "audioPlayer.play");
        call.Args.Take(4).Should().Equal("bg", $"{VaultFileRequest.Prefix}_audio/fondo/lluvia.mp3?v={File.GetLastWriteTimeUtc(_vault.PathOf(Track)).Ticks}", 0.5, true);
    }

    [Fact]
    public async Task Play_returns_false_when_js_reports_an_error()
    {
        _js.PlayResult = "NotAllowedError";

        (await _player.PlayAsync("bg", Track, 0.5, false)).Should().BeFalse();
    }

    [Fact]
    public async Task Play_of_a_missing_file_does_not_reach_js()
    {
        var played = await _player.PlayAsync("bg", "_audio/no-existe.mp3", 0.5, false);

        played.Should().BeFalse();
        _js.Calls.Should().BeEmpty();
    }

    [Fact]
    public void Ended_callback_raises_Ended()
    {
        string? ended = null;
        _player.Ended += (_, id) => ended = id;

        _player.OnEnded("fx");

        ended.Should().Be("fx");
    }

    [Fact]
    public void Failed_callback_raises_Failed()
    {
        string? failed = null;
        _player.Failed += (_, id) => failed = id;

        _player.OnFailed("fx");

        failed.Should().Be("fx");
    }

    [Fact]
    public async Task Js_disconnected_is_swallowed()
    {
        _js.Failure = new JSDisconnectedException("cerrado");

        var act = async () =>
        {
            await _player.SetVolumeAsync("bg", 0.2);
            await _player.StopAllAsync();
        };

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Js_failure_makes_play_return_false()
    {
        _js.Failure = new JSException("boom");

        (await _player.PlayAsync("bg", Track, 0.5, false)).Should().BeFalse();
    }

    [Fact]
    public async Task Play_superseded_by_another_play_is_not_a_failure()
    {
        _js.PlayResult = "superseded";

        (await _player.PlayAsync("bg", Track, 0.5, false)).Should().BeTrue();
    }

    [Fact]
    public async Task Two_plays_of_the_same_channel_share_the_dotnet_reference()
    {
        await _player.PlayAsync("bg", Track, 0.5, true);
        await _player.PlayAsync("bg", Track, 0.5, true);

        _js.Calls.Where(c => c.Identifier == "audioPlayer.play").Select(c => c.Args[4]).Distinct().Should().HaveCount(1);
    }

    [Fact]
    public async Task Dispose_stops_everything_in_js()
    {
        await _player.DisposeAsync();

        _js.Calls.Should().Contain(c => c.Identifier == "audioPlayer.stopAll");
    }

    [Fact]
    public async Task Play_after_dispose_returns_false()
    {
        await _player.DisposeAsync();

        (await _player.PlayAsync("bg", Track, 0.5, false)).Should().BeFalse();
    }

    [Fact]
    public async Task Play_after_dispose_does_not_call_js_play()
    {
        await _player.DisposeAsync();
        await _player.PlayAsync("bg", Track, 0.5, false);

        _js.Calls.Should().NotContain(c => c.Identifier == "audioPlayer.play");
    }

    [Fact]
    public async Task Callbacks_after_dispose_are_ignored()
    {
        var raised = 0;
        _player.Ended += (_, _) => raised++;
        _player.Failed += (_, _) => raised++;
        await _player.DisposeAsync();

        _player.OnEnded("fx");
        _player.OnFailed("fx");

        raised.Should().Be(0);
    }

    private sealed class RecordingJs : IJSRuntime
    {
        public List<(string Identifier, object?[] Args)> Calls { get; } = [];
        public Exception? Failure { get; set; }
        public string PlayResult { get; set; } = "ok";

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            if (Failure is not null) throw Failure;
            Calls.Add((identifier, args ?? []));
            return ValueTask.FromResult(typeof(TValue) == typeof(string) ? (TValue)(object)PlayResult : default!);
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
            InvokeAsync<TValue>(identifier, args);
    }
}
