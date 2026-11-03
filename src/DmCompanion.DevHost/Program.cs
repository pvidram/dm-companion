using DmCompanion.Core.Vault.Storage;
using DmCompanion.DevHost;
using DmCompanion.DevHost.Components;
using DmCompanion.DevHost.Platform;
using DmCompanion.DevHost.Services;
using DmCompanion.Infrastructure;
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
builder.Services.AddScoped<ShellState>();
builder.Services.AddScoped<WikilinkInterop>();
builder.Services.AddScoped<BackNavigation>();
builder.Services.AddSingleton<PortraitUrlProvider>();
builder.Services.AddSingleton<DevVaultSeeder>();

var app = builder.Build();

app.Lifetime.ApplicationStarted.Register(() =>
{
    var server = app.Services.GetRequiredService<IServer>();
    // Solo Kestrel abre sockets: un servidor en memoria (TestServer) no tiene direcciones que vigilar.
    if (server is not KestrelServer) return;

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
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(typeof(VaultDiagnostics).Assembly, typeof(DmCompanion.UI.Pages.Styleguide).Assembly);

app.Run();

public partial class Program;
