using DmCompanion.Core.Glossary;
using DmCompanion.Core.Vault.Storage;
using DmCompanion.DevHost;
using DmCompanion.DevHost.Components;
using DmCompanion.DevHost.Platform;
using DmCompanion.DevHost.Services;
using DmCompanion.Core.Tv;
using DmCompanion.Infrastructure;
using DmCompanion.Infrastructure.Tv;
using DmCompanion.Screens.Components;
using DmCompanion.Screens.Platform;
using DmCompanion.Screens.Services;
using DmCompanion.UI;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;

var builder = WebApplication.CreateBuilder(args);

// Solo loopback, siempre: el DevHost sirve el vault sin autenticación. Un Listen explícito
// anula ASPNETCORE_URLS y --urls, así que no se puede ampliar desde fuera.
builder.WebHost.ConfigureKestrel(options => options.ListenLocalhost(DevHostBinding.Port));

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddDmCompanionUI();
builder.Services.AddSingleton<IVaultServiceFactory, FileSystemVaultServiceFactory>();
builder.Services.AddSingleton<IFolderBrowser, DirectoryBrowser>();
builder.Services.AddSingleton<IAppPreferences>(sp =>
    new JsonFileAppPreferences(DevHostPaths.PreferencesFile, sp.GetRequiredService<ILogger<JsonFileAppPreferences>>()));
builder.Services.AddSingleton<IStorageAccess, DevStorageAccess>();
builder.Services.AddSingleton<VaultHost>();
builder.Services.AddSingleton<MapSession>();
builder.Services.AddSingleton<TokenHitPoints>();
builder.Services.AddSingleton<CombatController>();
builder.Services.AddSingleton<MapLibrary>();
builder.Services.AddSingleton<IGlossaryPackSource, EmbeddedGlossaryPackSource>();
builder.Services.AddSingleton<GlossaryPackLoader>();
builder.Services.AddSingleton<GlossaryService>();
builder.Services.AddSingleton<AudioLibraryService>();
builder.Services.AddSingleton<ManualLibrary>();
builder.Services.AddSingleton<ManualBookmarkStore>();
builder.Services.AddSingleton<ManualPositions>();
builder.Services.AddScoped<ManualViewerInterop>();
builder.Services.AddScoped<AudioMixer>();
builder.Services.AddSingleton<AudioStore>();
builder.Services.AddScoped<ShellState>();
builder.Services.AddScoped<WikilinkInterop>();
builder.Services.AddScoped<MapCanvasInterop>();
builder.Services.AddScoped<PlayerPreview>();
builder.Services.AddScoped<PlayerPreviewInterop>();
builder.Services.AddScoped<TurnStripInterop>();
builder.Services.AddScoped<DragPanelInterop>();
builder.Services.AddScoped<IAudioPlayer, WebAudioPlayer>();
builder.Services.AddScoped<BackNavigation>();
builder.Services.AddSingleton<VaultFileUrls>();
builder.Services.AddSingleton<NoteImageResolver>();
builder.Services.AddSingleton<PortraitUrlProvider>();
builder.Services.AddSingleton<JournalStore>();
builder.Services.AddSingleton<ITvAssets, EmbeddedTvAssets>();
builder.Services.AddSingleton<ITvServer, TvServer>();
builder.Services.AddSingleton<TvMapFeed>();
builder.Services.AddSingleton<TvContentFeed>();
builder.Services.AddSingleton<TvSession>();
builder.Services.AddSingleton<JournalIndex>();
builder.Services.AddSingleton<JournalLogService>();
builder.Services.AddSingleton<DevVaultSeeder>();

var app = builder.Build();

app.Lifetime.ApplicationStarted.Register(() =>
{
    var server = app.Services.GetRequiredService<IServer>();
    // Solo Kestrel abre sockets: un servidor en memoria (TestServer) no tiene direcciones que vigilar.
    if (!KestrelDetection.IsKestrel(server)) return;

    // Una feature ausente o vacía cuenta como no loopback: se detiene.
    var addresses = server.Features.Get<IServerAddressesFeature>()?.Addresses ?? [];
    if (DevHostBinding.IsLoopbackOnly(addresses)) return;

    app.Logger.LogCritical("El DevHost escucha fuera de loopback ({Addresses}). Se detiene: no tiene autenticación.", string.Join(", ", addresses));
    app.Lifetime.StopApplication();
});

await app.Services.GetRequiredService<DevVaultSeeder>().StartAsync();

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseAntiforgery();

app.MapStaticAssets();
app.MapVaultFiles();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(typeof(VaultDiagnostics).Assembly, typeof(DmCompanion.UI.Pages.Styleguide).Assembly);

app.Run();

public partial class Program;
