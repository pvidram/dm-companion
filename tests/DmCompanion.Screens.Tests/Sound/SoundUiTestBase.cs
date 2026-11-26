using Bunit;
using DmCompanion.Core.Audio;
using DmCompanion.Core.Vault;
using DmCompanion.Core.Vault.Storage;
using DmCompanion.Infrastructure;
using DmCompanion.Screens.Platform;
using DmCompanion.Screens.Services;
using DmCompanion.Screens.Tests.Support;
using DmCompanion.UI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace DmCompanion.Screens.Tests.Sound;

/// <summary>Vault temporal con audio y dos notas, servicios de sonido con reproductor falso y los del shell que usa NoteReader.</summary>
public abstract class SoundUiTestBase : BunitContext, IAsyncLifetime
{
    protected static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);
    protected const string Fondo = "fondo.mp3";
    protected const string Lluvia = "Bosque/lluvia.ogg";
    protected const string Cuervo = "Efectos/cuervo.mp3";

    private readonly TempVault _vault = TempVault.Empty();

    protected SoundUiTestBase()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddDmCompanionUI();
        Services.AddSingleton<IAppPreferences>(new InMemoryAppPreferences());
        Services.AddSingleton<IVaultServiceFactory>(new FileSystemVaultServiceFactory(NullLoggerFactory.Instance));
        Services.AddSingleton<VaultHost>();
        Services.AddSingleton<VaultFileUrls>();
        Services.AddSingleton<NoteImageResolver>();
        Services.AddSingleton<PortraitUrlProvider>();
        Services.AddScoped<ShellState>();
        Services.AddGlossaryForTests();
        Services.AddSingleton<TimeProvider>(Time);
        Services.AddSingleton<MapSession>();
        Services.AddSingleton<TokenHitPoints>();
        Services.AddSingleton<CombatController>();
        Services.AddSingleton<MapLibrary>();
        Services.AddScoped<MapCanvasInterop>();
        Services.AddScoped<TurnStripInterop>();
        Services.AddWindowingForTests();
        Services.AddScoped<WikilinkInterop>();
        Services.AddSoundForTests();
    }

    protected FakeTimeProvider Time { get; } = new();
    protected virtual bool WithAudio => true;
    protected FakeAudioPlayer Player => Services.GetRequiredService<FakeAudioPlayer>();
    protected AudioMixer Mixer => Services.GetRequiredService<AudioMixer>();
    protected AudioStore Store => Services.GetRequiredService<AudioStore>();
    protected ShellState Shell => Services.GetRequiredService<ShellState>();

    public async Task InitializeAsync()
    {
        foreach (var track in WithAudio ? new[] { Fondo, Lluvia, Cuervo } : [])
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_vault.PathOf("_audio/" + track))!);
            await File.WriteAllBytesAsync(_vault.PathOf("_audio/" + track), [1, 2, 3]);
        }
        Directory.CreateDirectory(_vault.PathOf("_audio/_dm/scenes"));
        await File.WriteAllTextAsync(_vault.PathOf("Taberna del Cuervo.md"), "---\ntype: location\nambience: Taberna\n---\nTexto.");
        await File.WriteAllTextAsync(_vault.PathOf("Sin ambiente.md"), "---\ntype: location\n---\nTexto.");

        await Services.GetRequiredService<VaultHost>().OpenAsync(_vault.Root);
        var library = Services.GetRequiredService<AudioLibraryService>();
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (WithAudio && library.Current.Tracks.Count < 3 && DateTime.UtcNow < deadline) await Task.Delay(25);
        if (WithAudio) library.Current.Tracks.Should().HaveCount(3);
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await Mixer.DisposeAsync();
        await Services.GetRequiredService<VaultHost>().DisposeAsync();
        await base.DisposeAsync();
        _vault.Dispose();
    }

    // Find y Click en el mismo turno del renderizador: un repintado por un cambio de biblioteca no deja el manejador a medias.
    protected static Task ClickAsync(IRenderedComponent<Microsoft.AspNetCore.Components.IComponent> cut, string selector) =>
        cut.InvokeAsync(() => cut.Find(selector).Click());

    protected VaultEntity Note(string relativePath) =>
        Services.GetRequiredService<VaultHost>().Current!.Index.Entities.Single(e => e.RelativePath == relativePath);

    protected Task<StoreResult> SaveSceneAsync(string name) =>
        Store.SaveSceneAsync(new AudioScene(name, new SceneBackground(new TrackBackground(Fondo), 0.5), [], 1.0), overwrite: false);
}
