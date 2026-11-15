using DmCompanion.Core.Audio;
using DmCompanion.Infrastructure;
using DmCompanion.Screens.Services;
using DmCompanion.Screens.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace DmCompanion.Screens.Tests;

/// <summary>Vault temporal con una biblioteca de audio pequeña, reproductor falso y reloj falso.</summary>
public abstract class AudioMixerTestBase : IAsyncLifetime
{
    protected const string Fondo = "fondo.mp3";
    protected const string Lluvia = "Bosque/lluvia.ogg";
    protected const string Trueno = "Efectos/trueno.wav";
    protected const string Cuervo = "Efectos/cuervo.mp3";
    protected static readonly string[] Taberna = ["Taberna/a.mp3", "Taberna/b.mp3", "Taberna/c.mp3"];

    private readonly TempVault _vault = TempVault.Empty();
    private readonly TempVault _otherVault = TempVault.Empty();
    private AudioLibraryService? _library;

    protected AudioMixerTestBase()
    {
        Host = new VaultHost(new FileSystemVaultServiceFactory(NullLoggerFactory.Instance), new InMemoryAppPreferences(), NullLogger<VaultHost>.Instance);
    }

    protected VaultHost Host { get; }
    protected FakeAudioPlayer Player { get; } = new();
    protected FakeTimeProvider Time { get; } = new();
    protected AudioMixer Mixer { get; private set; } = null!;
    protected string OtherVaultRoot => _otherVault.Root;

    public async Task InitializeAsync()
    {
        foreach (var track in new[] { Fondo, Lluvia, Trueno, Cuervo }.Concat(Taberna))
            WriteAudio("_audio/" + track);
        Directory.CreateDirectory(_vault.PathOf("_audio/Vacia"));
        File.WriteAllText(_vault.PathOf("_audio/Vacia/notas.txt"), "sin audio");

        await Host.OpenAsync(_vault.Root);
        _library = new AudioLibraryService(Host, NullLogger<AudioLibraryService>.Instance);
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (_library.Current.Tracks.Count < 7 && DateTime.UtcNow < deadline) await Task.Delay(25);
        _library.Current.Tracks.Should().HaveCount(7);

        Mixer = new AudioMixer(Player, _library, Host, Time, NullLogger<AudioMixer>.Instance, new Random(1));
    }

    public async Task DisposeAsync()
    {
        await Mixer.DisposeAsync();
        _library?.Dispose();
        await Host.DisposeAsync();
        _vault.Dispose();
        _otherVault.Dispose();
    }

    protected void Advance(double seconds) => Time.Advance(TimeSpan.FromSeconds(seconds));

    protected FakeAudioPlayer.Channel? ChannelOf(string track) =>
        Player.Channels.Values.SingleOrDefault(c => c.Path == AudioPaths.ToVaultPath(track));

    protected string ChannelIdOf(string track) =>
        Player.Channels.Single(c => c.Value.Path == AudioPaths.ToVaultPath(track)).Key;

    protected double? VolumeOf(string track) => ChannelOf(track) is { } channel ? Math.Round(channel.Volume, 3) : null;

    protected IReadOnlyList<string> PlayingPaths() => [.. Player.Channels.Values.Select(c => AudioPaths.ToLibraryPath(c.Path)).Order()];

    protected void EndBackgroundTrack() => Player.RaiseEnded(ChannelIdOf(Mixer.State.Background!.CurrentTrack!));

    private void WriteAudio(string relative)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_vault.PathOf(relative))!);
        File.WriteAllBytes(_vault.PathOf(relative), [1, 2, 3]);
    }
}
