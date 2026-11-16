using Bunit;
using DmCompanion.Core.Audio;
using DmCompanion.Core.Vault.Storage;
using DmCompanion.Infrastructure;
using DmCompanion.Screens.Components;
using DmCompanion.Screens.Platform;
using DmCompanion.Screens.Services;
using DmCompanion.Screens.Tests.Support;
using DmCompanion.UI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace DmCompanion.Screens.Tests.Sound;

/// <summary>Vault temporal con una biblioteca pequeña, reproductor y reloj falsos, y la pantalla «Sonido» abierta en el shell.</summary>
public abstract class SoundTestContext : BunitContext, IAsyncLifetime
{
    protected const string Lluvia = "Bosque/lluvia.ogg";
    protected const string Trueno = "Efectos/trueno.wav";
    protected static readonly TimeSpan Wait = TimeSpan.FromSeconds(10);

    private readonly TempVault _vault = TempVault.Empty();

    protected SoundTestContext()
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
        Services.AddSingleton<TimeProvider>(new FakeTimeProvider());
        Services.AddSingleton<MapSession>();
        Services.AddSingleton<TokenHitPoints>();
        Services.AddSingleton<CombatController>();
        Services.AddSingleton<MapLibrary>();
        Services.AddScoped<MapCanvasInterop>();
        Services.AddScoped<TurnStripInterop>();
        Services.AddScoped<DragPanelInterop>();
        Services.AddScoped<WikilinkInterop>();
        Services.AddSingleton<FakeAudioPlayer>();
        Services.AddSingleton<IAudioPlayer>(sp => sp.GetRequiredService<FakeAudioPlayer>());
        Services.AddSingleton<AudioLibraryService>();
        Services.AddSingleton<AudioStore>();
        Services.AddSingleton<AudioMixer>();

        foreach (var track in new[] { "Taberna/a.mp3", "Taberna/b.mp3", Lluvia, Trueno }) WriteBytes("_audio/" + track);
        Write("Historia/Uno.md", "---\ntype: note\nname: Uno\nambience: Taberna\n---\nTexto.\n");
        Write("Historia/Dos.md", "---\ntype: note\nname: Dos\nambience: Taberna\n---\nTexto.\n");
    }

    protected ShellState Shell => Services.GetRequiredService<ShellState>();
    protected AudioMixer Mixer => Services.GetRequiredService<AudioMixer>();
    protected AudioStore Store => Services.GetRequiredService<AudioStore>();

    async Task IAsyncLifetime.InitializeAsync()
    {
        await Services.GetRequiredService<VaultHost>().OpenAsync(_vault.Root);
        var library = Services.GetRequiredService<AudioLibraryService>();
        var deadline = DateTime.UtcNow + Wait;
        while (library.Current.Tracks.Count < 4 && DateTime.UtcNow < deadline) await Task.Delay(25);
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await Mixer.DisposeAsync();
        Services.GetRequiredService<AudioLibraryService>().Dispose();
        Store.Dispose();
        await Services.GetRequiredService<VaultHost>().DisposeAsync();
        await base.DisposeAsync();
        _vault.Dispose();
    }

    protected string PathOf(string path) => _vault.PathOf(path);

    protected void Write(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_vault.PathOf(path))!);
        File.WriteAllText(_vault.PathOf(path), text);
    }

    protected IRenderedComponent<DmShell> RenderOpenSound()
    {
        var cut = Render<DmShell>();
        cut.InvokeAsync(() => Shell.Toggle(OverlayPanel.Sound)).GetAwaiter().GetResult();
        cut.WaitForAssertion(() => cut.FindAll("[data-testid=sound-section]").Should().HaveCount(3), Wait);
        return cut;
    }

    // El watcher del vault puede repintar la biblioteca entre buscar y pulsar: se reintenta con el DOM nuevo.
    protected static void ClickWhenReady(IRenderedComponent<DmShell> cut, string selector) =>
        cut.WaitForAssertion(() => cut.Find(selector).Click(), Wait);

    protected static void Expand(IRenderedComponent<DmShell> cut, string section) =>
        cut.WaitForAssertion(() => cut.FindAll("[data-testid=sound-section] .ds-list-row")
            .First(row => row.TextContent.Trim().StartsWith(section, StringComparison.Ordinal)).Click(), Wait);

    protected static AngleSharp.Dom.IElement OpenDialog(IRenderedComponent<DmShell> cut)
    {
        cut.WaitForElement(".ds-dialog-backdrop:not([hidden]) .ds-dialog", Wait);
        return cut.Find(".ds-dialog-backdrop:not([hidden]) .ds-dialog");
    }

    protected async Task SaveSceneAsync(string name, AudioScene? scene = null) =>
        (await Store.SaveSceneAsync(scene ?? new AudioScene(name, new SceneBackground(new TrackBackground(Lluvia), 0.5), [], 1.0), overwrite: false))
            .Should().Be(StoreResult.Ok);

    private void WriteBytes(string relative)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_vault.PathOf(relative))!);
        File.WriteAllBytes(_vault.PathOf(relative), [1, 2, 3]);
    }
}
