using DmCompanion.Infrastructure;
using DmCompanion.Screens.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace DmCompanion.Screens.Tests;

public sealed class AudioLibraryServiceTests : IAsyncDisposable
{
    private readonly TempVault _vault = TempVault.Empty();
    private readonly VaultHost _host = new(new FileSystemVaultServiceFactory(NullLoggerFactory.Instance), new InMemoryAppPreferences(), NullLogger<VaultHost>.Instance);

    public async ValueTask DisposeAsync()
    {
        await _host.DisposeAsync();
        _vault.Dispose();
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition() && DateTime.UtcNow < deadline) await Task.Delay(50);
        condition().Should().BeTrue();
    }

    private void WriteAudio(string relative)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_vault.PathOf(relative))!);
        File.WriteAllBytes(_vault.PathOf(relative), [1, 2, 3]);
    }

    [Fact]
    public async Task Service_lists_audio_when_the_vault_opens()
    {
        WriteAudio("_audio/Taberna/a.mp3");
        using var service = new AudioLibraryService(_host, NullLogger<AudioLibraryService>.Instance);

        await _host.OpenAsync(_vault.Root);

        await WaitForAsync(() => service.Current.Tracks.Count == 1);
    }

    [Fact]
    public async Task Service_rebuilds_when_a_file_under_audio_changes()
    {
        Directory.CreateDirectory(_vault.PathOf("_audio"));
        await _host.OpenAsync(_vault.Root);
        using var service = new AudioLibraryService(_host, NullLogger<AudioLibraryService>.Instance);

        WriteAudio("_audio/nuevo.ogg");

        await WaitForAsync(() => service.Current.FindTrack("nuevo.ogg") is not null);
    }

    [Fact]
    public async Task Service_is_empty_when_the_vault_closes()
    {
        WriteAudio("_audio/a.mp3");
        await _host.OpenAsync(_vault.Root);
        using var service = new AudioLibraryService(_host, NullLogger<AudioLibraryService>.Instance);
        await WaitForAsync(() => service.Current.Tracks.Count == 1);

        await _host.ForgetAsync();

        await WaitForAsync(() => service.Current.Tracks.Count == 0);
    }
}
