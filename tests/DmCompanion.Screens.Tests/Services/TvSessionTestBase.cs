using System.Text.Json;
using DmCompanion.Core.Tv;
using DmCompanion.Core.Vault;
using DmCompanion.Core.Vault.Storage;
using DmCompanion.Infrastructure;
using DmCompanion.Screens.Services;
using DmCompanion.Screens.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;

namespace DmCompanion.Screens.Tests;

/// <summary>TvSession sobre una copia del vault de demo, con la TV activada y un servidor en memoria.</summary>
public abstract class TvSessionTestBase : IAsyncLifetime
{
    protected const string Cave = "Mapas/Boceto de la cueva.md";
    protected const string Lighthouse = "Mapas/Plano del Faro.md";
    protected const string OldLighthouse = "Lugares/Faro Viejo.md";
    protected static readonly TvFrame Square = new(0, 0, 900, 900);

    private readonly TempVault _vault = TempVault.FromDemo();
    private readonly InMemoryAppPreferences _prefs = new();

    protected TvSessionTestBase()
    {
        Host = new VaultHost(new FileSystemVaultServiceFactory(NullLoggerFactory.Instance), _prefs, NullLogger<VaultHost>.Instance);
        Maps = new MapSession(Host, _prefs, TimeProvider.System, NullLogger<MapSession>.Instance);
        Library = new MapLibrary(Host, Maps);
        var images = new NoteImageResolver(Host);
        var mapFeed = new TvMapFeed(Host, new TokenHitPoints(Host, GlossaryTestServices.CreateGlossary(Host)), images);
        var contentFeed = new TvContentFeed(Host, images, mapFeed);
        Tv = new TvSession(Server, Host, Maps, mapFeed, contentFeed, _prefs, NullLogger<TvSession>.Instance);
    }

    private protected FakeTvServer Server { get; } = new();
    protected VaultHost Host { get; }
    protected MapSession Maps { get; }
    protected MapLibrary Library { get; }
    protected TvSession Tv { get; }

    async Task IAsyncLifetime.InitializeAsync()
    {
        await Host.OpenAsync(_vault.Root);
        await Tv.EnableAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await Maps.DisposeAsync();
        await Host.DisposeAsync();
        _vault.Dispose();
    }

    protected string VaultPathOf(string relativePath) => _vault.PathOf(relativePath);

    protected JsonElement LastMessage => JsonDocument.Parse(Server.Published[^1]).RootElement;

    protected string? LastKind => LastMessage.GetProperty("kind").GetString();

    protected Task SaveNoteAsync(string path, string content) =>
        Host.Current!.SaveAsync(path, content, WritePrecondition.Overwrite);

    protected async Task FixOnCaveAsync()
    {
        await Maps.OpenAsync(Cave);
        await Tv.FixFrameAsync(Cave, Square);
    }

    protected static JsonElement? TokenIn(JsonElement map, string id) =>
        map.GetProperty("tokens").EnumerateArray().Cast<JsonElement?>().FirstOrDefault(t => t!.Value.GetProperty("id").GetString() == id);

    /// <summary>
    /// Borra un fichero que no es una nota y espera a que el watcher lo notifique. Los suscriptores de FileChanged se llaman en el
    /// orden de suscripción, así que cuando llega aquí el resolvedor de imágenes y la TV ya lo han visto (la lista queda obsoleta).
    /// </summary>
    protected async Task DeleteAndAwaitWatcherAsync(string path)
    {
        var vault = Host.Current!;
        var seen = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnChanged(object? sender, VaultChange change)
        {
            if (string.Equals(VaultPaths.Normalize(change.RelativePath), path, StringComparison.OrdinalIgnoreCase)) seen.TrySetResult();
        }
        vault.FileChanged += OnChanged;
        try
        {
            await vault.DeleteAsync(path);
            await seen.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally
        {
            vault.FileChanged -= OnChanged;
        }
    }

    protected static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException();
            await Task.Delay(20);
        }
    }
}
